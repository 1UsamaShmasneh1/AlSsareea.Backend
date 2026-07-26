using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace AlSsareea.Modules.Catalog.Infrastructure.Persistence.DesignTime;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) { string connection = Environment.GetEnvironmentVariable("ALSSAREEA_CATALOG_DATABASE") ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password"; DbContextOptionsBuilder<CatalogDbContext> b = new(); b.UseNpgsql(connection, n => n.MigrationsHistoryTable(CatalogPersistence.MigrationsHistoryTable, CatalogPersistence.Schema)).UseSnakeCaseNamingConvention(); return new CatalogDbContext(b.Options); }
}
