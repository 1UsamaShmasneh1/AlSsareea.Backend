using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlSsareea.Modules.Orders.Infrastructure.Persistence.DesignTime;

public sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        string connection = Environment.GetEnvironmentVariable("ConnectionStrings__OrdersDatabase")
            ?? Environment.GetEnvironmentVariable("ALSSAREEA_ORDERS_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=alssareea;Username=alssareea;Password=alssareea_dev_password";
        DbContextOptions<OrdersDbContext> options = new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName).MigrationsHistoryTable(OrdersPersistence.MigrationsHistoryTable, OrdersPersistence.Schema)).UseSnakeCaseNamingConvention().Options;
        return new(options);
    }
}
