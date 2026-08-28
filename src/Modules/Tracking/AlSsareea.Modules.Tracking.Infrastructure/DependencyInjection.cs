using AlSsareea.Modules.Tracking.Application;
using AlSsareea.Modules.Tracking.Contracts;
using AlSsareea.Modules.Tracking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AlSsareea.Modules.Tracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString(TrackingPersistence.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("DriversDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:TrackingDatabase is required.");
        services.AddOptions<TrackingOptions>().Bind(configuration.GetSection(TrackingOptions.SectionName));
        services.AddDbContext<TrackingDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite().MigrationsAssembly(typeof(TrackingDbContext).Assembly.FullName).MigrationsHistoryTable(TrackingPersistence.MigrationsHistoryTable, TrackingPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<TrackingDbContext>("tracking-postgresql", tags: ["ready"]);
        services.AddScoped<TrackingStore>(); services.AddScoped<ITrackingStore>(x => x.GetRequiredService<TrackingStore>()); services.AddScoped<IDispatchLocationProvider>(x => x.GetRequiredService<TrackingStore>()); services.AddScoped<ITrackingService, TrackingService>();
        services.TryAddScoped<ILocationRealtimePublisher, NullLocationRealtimePublisher>(); services.TryAddScoped<ITrackingVisibilityProvider, UnavailableTrackingVisibilityProvider>();
        return services;
    }
}
