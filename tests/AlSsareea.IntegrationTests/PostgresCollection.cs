namespace AlSsareea.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PostgresTestSuite : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL/PostGIS";
}
