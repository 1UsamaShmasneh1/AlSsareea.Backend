using AlSsareea.Modules.Delivery.Application;
using AlSsareea.Modules.Delivery.Infrastructure.Persistence;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlSsareea.Modules.Delivery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeliveryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString(DeliveryPersistence.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("TrackingDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:DeliveryDatabase is required.");
        services.AddDbContext<DeliveryDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(DeliveryDbContext).Assembly.FullName).MigrationsHistoryTable(DeliveryPersistence.MigrationsHistoryTable, DeliveryPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<DeliveryDbContext>("delivery-postgresql", tags: ["ready"]);
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        services.AddScoped<IDeliveryPinProtector, DeliveryPinProtector>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.Replace(ServiceDescriptor.Scoped<ITrackingVisibilityProvider, DeliveryTrackingVisibilityProvider>());
        return services;
    }
}
