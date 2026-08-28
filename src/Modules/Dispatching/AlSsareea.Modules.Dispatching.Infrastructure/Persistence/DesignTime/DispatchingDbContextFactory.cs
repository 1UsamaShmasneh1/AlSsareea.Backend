using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Dispatching.Infrastructure.Persistence.DesignTime;

public sealed class DispatchingDbContextFactory : IDesignTimeDbContextFactory<DispatchingDbContext>
{
    public DispatchingDbContext CreateDbContext(string[] args) { string connection = Environment.GetEnvironmentVariable("ALSSAREEA_DISPATCHING_DESIGN_CONNECTION") ?? "Host=localhost;Port=5432;Database=alssareea;Username=postgres;Password=postgres"; DbContextOptionsBuilder<DispatchingDbContext> builder = new(); builder.UseNpgsql(connection, n => n.MigrationsHistoryTable(DispatchingPersistence.MigrationsHistoryTable, DispatchingPersistence.Schema)).UseSnakeCaseNamingConvention(); return new(builder.Options); }
}
