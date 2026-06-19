using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ReplyInMyVoice.Infrastructure.Data;

namespace ReplyInMyVoice.Tests.ApiKeyPepperRotation;

public sealed class ApiKeyPepperRotationMigrationTests
{
    [Fact]
    public void Database_migration_adds_pepper_columns_with_backward_compatible_defaults()
    {
        var migrationType = typeof(AppDbContext).Assembly
            .GetTypes()
            .SingleOrDefault(type =>
                typeof(Migration).IsAssignableFrom(type) &&
                type.Name.Contains("AddApiKeyPepperRotation", StringComparison.Ordinal));

        migrationType.Should().NotBeNull();
        var operations = BuildOperations(migrationType!, "Up");

        var pepperVersion = operations.OfType<AddColumnOperation>()
            .Should()
            .ContainSingle(operation =>
                operation.Table == "ApiKeys" &&
                operation.Name == "PepperVersion")
            .Subject;
        pepperVersion.ClrType.Should().Be(typeof(int));
        pepperVersion.IsNullable.Should().BeFalse();
        pepperVersion.DefaultValue.Should().Be(1);

        var rehashPending = operations.OfType<AddColumnOperation>()
            .Should()
            .ContainSingle(operation =>
                operation.Table == "ApiKeys" &&
                operation.Name == "RehashPending")
            .Subject;
        rehashPending.ClrType.Should().Be(typeof(bool));
        rehashPending.IsNullable.Should().BeFalse();
        rehashPending.DefaultValue.Should().Be(false);
    }

    private static IReadOnlyList<MigrationOperation> BuildOperations(Type migrationType, string methodName)
    {
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var method = migrationType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        method!.Invoke(migration, [builder]);
        return builder.Operations;
    }
}
