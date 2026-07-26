using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Media.Infrastructure.Persistence.DesignTime;

public sealed class MediaDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__MediaDatabase") ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        var builder = new DbContextOptionsBuilder<MediaDbContext>();
        builder.UseNpgsql(connection, n => n.MigrationsHistoryTable(MediaPersistence.MigrationsHistoryTable, MediaPersistence.Schema)).UseSnakeCaseNamingConvention();
        return new MediaDbContext(builder.Options);
    }
}
