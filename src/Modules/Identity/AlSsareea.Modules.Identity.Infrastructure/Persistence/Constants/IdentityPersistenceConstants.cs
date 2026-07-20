namespace AlSsareea.Modules.Identity.Infrastructure.Persistence.Constants;

internal static class IdentityPersistenceConstants
{
    internal const string Schema = "identity";
    internal const string MigrationsHistoryTable = "__ef_migrations_history";
    internal const string ConnectionStringName = "IdentityDatabase";
    internal const string DevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password;Include Error Detail=true";
}
