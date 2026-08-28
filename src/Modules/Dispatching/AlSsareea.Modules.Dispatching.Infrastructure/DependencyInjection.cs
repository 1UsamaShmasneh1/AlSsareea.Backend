using AlSsareea.Modules.Dispatching.Application;
using AlSsareea.Modules.Dispatching.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Dispatching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDispatchingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString(DispatchingPersistence.ConnectionStringName); if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("DeliveryDatabase"); if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:DispatchingDatabase is required.");
        services.AddOptions<DispatchingOptions>().Bind(configuration.GetSection(DispatchingOptions.SectionName)); services.AddDbContext<DispatchingDbContext>(options => options.UseNpgsql(connection, n => n.MigrationsAssembly(typeof(DispatchingDbContext).Assembly.FullName).MigrationsHistoryTable(DispatchingPersistence.MigrationsHistoryTable, DispatchingPersistence.Schema)).UseSnakeCaseNamingConvention()); services.AddHealthChecks().AddDbContextCheck<DispatchingDbContext>("dispatching-postgresql", tags: ["ready"]); services.AddScoped<IDispatchRepository, DispatchRepository>(); services.AddScoped<IDispatchService, DispatchService>(); services.AddHostedService<DispatchExpirationWorker>(); return services;
    }
}
