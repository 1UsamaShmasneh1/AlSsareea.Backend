using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Drivers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDriversInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString(DriversPersistence.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("OrdersDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:DriversDatabase is required.");
        services.AddDbContext<DriversDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(DriversDbContext).Assembly.FullName).MigrationsHistoryTable(DriversPersistence.MigrationsHistoryTable, DriversPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<DriversDbContext>("drivers-postgresql", tags: ["ready"]);
        services.AddScoped<IDriverRepository, DriverRepository>(); services.AddScoped<DriverService>(); services.AddScoped<IDriverService>(x => x.GetRequiredService<DriverService>()); services.AddScoped<IDriverEligibilityProvider>(x => x.GetRequiredService<DriverService>()); services.AddScoped<IDriverOperationalSnapshotProvider>(x => x.GetRequiredService<DriverService>()); services.AddScoped<IDriverDispatchCandidateProvider, DriverDispatchCandidateProvider>();
        return services;
    }
}
