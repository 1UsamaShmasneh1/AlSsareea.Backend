using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Drivers.Infrastructure.Persistence.DesignTime;

public sealed class DriversDbContextFactory : IDesignTimeDbContextFactory<DriversDbContext>
{
    public DriversDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__DriversDatabase") ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea";
        DbContextOptions<DriversDbContext> options = new DbContextOptionsBuilder<DriversDbContext>().UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(DriversDbContext).Assembly.FullName).MigrationsHistoryTable(DriversPersistence.MigrationsHistoryTable, DriversPersistence.Schema)).UseSnakeCaseNamingConvention().Options;
        return new DriversDbContext(options);
    }
}
