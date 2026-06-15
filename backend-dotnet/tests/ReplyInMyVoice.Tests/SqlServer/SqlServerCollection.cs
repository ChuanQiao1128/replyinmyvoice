namespace ReplyInMyVoice.Tests.SqlServer;

[CollectionDefinition("SqlServer")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerDbFixture>
{
    public const string Name = "SqlServer";
}
