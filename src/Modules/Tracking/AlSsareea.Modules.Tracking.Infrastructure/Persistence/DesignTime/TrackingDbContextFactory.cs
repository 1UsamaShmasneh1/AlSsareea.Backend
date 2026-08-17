using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Tracking.Infrastructure.Persistence.DesignTime;

public sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__TrackingDatabase") ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea";
        DbContextOptions<TrackingDbContext> options = new DbContextOptionsBuilder<TrackingDbContext>().UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite().MigrationsAssembly(typeof(TrackingDbContext).Assembly.FullName).MigrationsHistoryTable(TrackingPersistence.MigrationsHistoryTable, TrackingPersistence.Schema)).UseSnakeCaseNamingConvention().Options;
        return new(options);
    }
}
