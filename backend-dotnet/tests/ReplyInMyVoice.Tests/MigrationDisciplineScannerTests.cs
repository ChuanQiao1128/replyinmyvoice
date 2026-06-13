using FluentAssertions;
using ReplyInMyVoice.MigrationGuard;

namespace ReplyInMyVoice.Tests;

public sealed class MigrationDisciplineScannerTests
{
    [Fact]
    public void Scan_AllCheckedInMigrations_HaveNoDestructiveFindings()
    {
        var migrationsDirectory = FindBackendRoot()
            .GetDirectories("src", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("ReplyInMyVoice.Infrastructure", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Migrations", SearchOption.TopDirectoryOnly)
            .Single();

        var migrationFiles = migrationsDirectory
            .EnumerateFiles("*.cs")
            .Where(file =>
                !file.Name.EndsWith(".Designer.cs", StringComparison.Ordinal) &&
                !string.Equals(file.Name, "AppDbContextModelSnapshot.cs", StringComparison.Ordinal))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();

        migrationFiles.Should().HaveCountGreaterThanOrEqualTo(25);

        foreach (var file in migrationFiles)
        {
            var result = MigrationDisciplineScanner.ScanFile(file.FullName, File.ReadAllText(file.FullName));

            result.Findings.Should().BeEmpty(file.Name);
        }
    }

    [Fact]
    public void Scan_DropTableInUp_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.DropTable(
                        name: "OldRows");
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "DropTable");
        result.IsViolation.Should().BeTrue();
    }

    [Fact]
    public void Scan_DropColumnInUp_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.DropColumn(
                        name: "LegacyValue",
                        table: "Users");
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "DropColumn");
        result.IsViolation.Should().BeTrue();
    }

    [Fact]
    public void Scan_RenameColumnInUp_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.RenameColumn(
                        name: "LegacyValue",
                        table: "Users",
                        newName: "CurrentValue");
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "RenameColumn");
        result.IsViolation.Should().BeTrue();
    }

    [Fact]
    public void Scan_RenameTableInUp_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.RenameTable(
                        name: "LegacyUsers",
                        newName: "Users");
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "RenameTable");
        result.IsViolation.Should().BeTrue();
    }

    [Fact]
    public void Scan_DropOperationsInDownOnly_AreClean()
    {
        var result = Scan(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Fixture.Migrations;

            public partial class FixtureMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.CreateTable(
                        name: "NewRows",
                        columns: table => new
                        {
                            Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                        },
                        constraints: table =>
                        {
                            table.PrimaryKey("PK_NewRows", x => x.Id);
                        });
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(
                        name: "LegacyValue",
                        table: "Users");
                    migrationBuilder.DropTable(
                        name: "NewRows");
                }
            }
            """);

        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Scan_AlterColumnLooseningNullability_IsClean()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<string>(
                        name: "ContentRetentionPolicy",
                        table: "Users",
                        type: "nvarchar(64)",
                        maxLength: 64,
                        nullable: true,
                        oldClrType: typeof(string),
                        oldType: "nvarchar(64)",
                        oldMaxLength: 64);
            """);

        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Scan_AlterColumnClrTypeChange_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<int>(
                        name: "Score",
                        table: "Users",
                        type: "int",
                        nullable: false,
                        oldClrType: typeof(string),
                        oldType: "nvarchar(max)");
            """);

        result.Findings.Should().ContainSingle(finding =>
            finding.Operation == "AlterColumn" &&
            finding.Reason == "CLR type change");
    }

    [Fact]
    public void Scan_AlterColumnStoreTypeChange_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<string>(
                        name: "DisplayName",
                        table: "Users",
                        type: "nvarchar(100)",
                        nullable: false,
                        oldClrType: typeof(string),
                        oldType: "nvarchar(max)");
            """);

        result.Findings.Should().ContainSingle(finding =>
            finding.Operation == "AlterColumn" &&
            finding.Reason == "store type change");
    }

    [Fact]
    public void Scan_AlterColumnMaxLengthNarrowed_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<string>(
                        name: "DisplayName",
                        table: "Users",
                        type: "nvarchar(max)",
                        maxLength: 64,
                        nullable: false,
                        oldClrType: typeof(string),
                        oldType: "nvarchar(max)",
                        oldMaxLength: 256);
            """);

        result.Findings.Should().ContainSingle(finding =>
            finding.Operation == "AlterColumn" &&
            finding.Reason == "max length narrowed");
    }

    [Fact]
    public void Scan_AlterColumnNullabilityTightened_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<string>(
                        name: "DisplayName",
                        table: "Users",
                        type: "nvarchar(max)",
                        nullable: false,
                        oldClrType: typeof(string),
                        oldType: "nvarchar(max)",
                        oldNullable: true);
            """);

        result.Findings.Should().ContainSingle(finding =>
            finding.Operation == "AlterColumn" &&
            finding.Reason == "nullability tightened");
    }

    [Fact]
    public void Scan_AlterColumnMissingOldTypePair_IsFlagged()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<string>(
                        name: "DisplayName",
                        table: "Users",
                        type: "nvarchar(100)",
                        nullable: false);
            """);

        result.Findings.Should().ContainSingle(finding =>
            finding.Operation == "AlterColumn" &&
            finding.Reason == "store type indeterminate");
    }

    [Fact]
    public void Scan_StringLiteralParensInsideAlterColumn_AreParsedCorrectly()
    {
        var result = ScanUp(
            """
                    migrationBuilder.AlterColumn<string>(
                        name: "Body",
                        table: "Messages",
                        type: "nvarchar(max)",
                        nullable: true,
                        oldClrType: typeof(string),
                        oldType: "nvarchar(max)");
            """);

        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Scan_DestructiveOpWithRiskMarker_IsAccepted()
    {
        var result = Scan(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            // MIGRATION-RISK-ACCEPTED: contract phase of HARD-12
            namespace Fixture.Migrations;

            public partial class FixtureMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(
                        name: "LegacyValue",
                        table: "Users");
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "DropColumn");
        result.HasRiskMarker.Should().BeTrue();
        result.MarkerReason.Should().Be("contract phase of HARD-12");
        result.IsViolation.Should().BeFalse();
    }

    [Fact]
    public void Scan_RiskMarkerWithoutReason_IsNotAccepted()
    {
        var result = Scan(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            // MIGRATION-RISK-ACCEPTED:
            namespace Fixture.Migrations;

            public partial class FixtureMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(
                        name: "LegacyValue",
                        table: "Users");
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "DropColumn");
        result.HasRiskMarker.Should().BeFalse();
        result.IsViolation.Should().BeTrue();
    }

    [Fact]
    public void Scan_CommentedOutDropColumn_IsClean()
    {
        var result = ScanUp(
            """
                    // migrationBuilder.DropColumn(
                    //     name: "LegacyValue",
                    //     table: "Users");
            """);

        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Scan_FileWithoutUpMethod_IsScannedWhole()
    {
        var result = Scan(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Fixture.Migrations;

            public sealed class Scratch
            {
                public void Run(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropTable(
                        name: "LegacyRows");
                }
            }
            """);

        result.Findings.Should().ContainSingle(finding => finding.Operation == "DropTable");
        result.IsViolation.Should().BeTrue();
    }

    private static MigrationScanResult ScanUp(string upContent)
    {
        return Scan($$"""
            using Microsoft.EntityFrameworkCore.Migrations;

            namespace Fixture.Migrations;

            public partial class FixtureMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
            {{upContent}}
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);
    }

    private static MigrationScanResult Scan(string content)
    {
        return MigrationDisciplineScanner.ScanFile("FixtureMigration.cs", content);
    }

    private static DirectoryInfo FindBackendRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ReplyInMyVoice.sln")))
            {
                return directory;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ReplyInMyVoice.sln from the test output directory.");
    }
}
