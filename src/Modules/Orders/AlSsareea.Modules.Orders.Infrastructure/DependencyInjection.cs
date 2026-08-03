using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString(OrdersPersistence.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("CartsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:OrdersDatabase is required.");
        services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(OrdersDbContext).Assembly.FullName).MigrationsHistoryTable(OrdersPersistence.MigrationsHistoryTable, OrdersPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>("orders-postgresql", tags: ["ready"]);
        services.AddScoped<IOrderRepository, OrderRepository>(); services.AddScoped<IOrderService, OrderService>(); services.AddScoped<IMerchantOrderService, MerchantOrderService>(); return services;
    }
}
