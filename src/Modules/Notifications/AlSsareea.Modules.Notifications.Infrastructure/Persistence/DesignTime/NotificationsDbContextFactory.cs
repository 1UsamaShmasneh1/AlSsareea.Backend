using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Notifications.Infrastructure.Persistence.DesignTime;

public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args) { string connection = Environment.GetEnvironmentVariable("ALSSAREEA_NOTIFICATIONS_DESIGN_CONNECTION") ?? "Host=localhost;Port=5432;Database=alssareea;Username=postgres;Password=postgres"; DbContextOptionsBuilder<NotificationsDbContext> builder = new(); builder.UseNpgsql(connection, n => n.MigrationsHistoryTable(NotificationsPersistence.MigrationsHistoryTable, NotificationsPersistence.Schema)).UseSnakeCaseNamingConvention(); return new(builder.Options); }
}
