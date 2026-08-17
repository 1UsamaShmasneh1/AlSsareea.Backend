using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Delivery.Infrastructure.Persistence.DesignTime;

public sealed class DeliveryDbContextFactory : IDesignTimeDbContextFactory<DeliveryDbContext>
{
    public DeliveryDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__DeliveryDatabase")
            ?? Environment.GetEnvironmentVariable("ALSSAREEA_DELIVERY_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        DbContextOptions<DeliveryDbContext> options = new DbContextOptionsBuilder<DeliveryDbContext>().UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(DeliveryDbContext).Assembly.FullName).MigrationsHistoryTable(DeliveryPersistence.MigrationsHistoryTable, DeliveryPersistence.Schema)).UseSnakeCaseNamingConvention().Options;
        return new(options);
    }
}
