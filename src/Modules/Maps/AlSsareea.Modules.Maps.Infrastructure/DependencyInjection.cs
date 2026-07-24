using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Maps.Application;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Maps.Infrastructure.Configuration;
using AlSsareea.Modules.Maps.Infrastructure.Persistence;
using AlSsareea.Modules.Maps.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Maps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMapsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MapsOptions>()
            .Bind(configuration.GetSection(MapsOptions.SectionName))
            .Validate(options => options.Provider == MapsProvider.Fake, "Unsupported maps provider.")
            .ValidateOnStart();

        services.AddDbContext<MapsDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString("MapsDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'MapsDatabase' is required when Maps persistence is used.");

            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.UseNetTopologySuite());
        });

        services.AddScoped<IServiceAreaRepository, ServiceAreaRepository>();
        services.AddScoped<IUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<MapsDbContext>());

        services.AddSingleton<FakeMapsProvider>();
        services.AddSingleton<IMapsProvider>(serviceProvider =>
            ResolveProvider(serviceProvider, configuration));
        services.AddSingleton<IGeocodingProvider>(
            serviceProvider => serviceProvider.GetRequiredService<IMapsProvider>());
        services.AddSingleton<IReverseGeocodingProvider>(
            serviceProvider => serviceProvider.GetRequiredService<IMapsProvider>());
        services.AddSingleton<IPlacesProvider>(
            serviceProvider => serviceProvider.GetRequiredService<IMapsProvider>());
        services.AddSingleton<IRoutingProvider>(
            serviceProvider => serviceProvider.GetRequiredService<IMapsProvider>());

        return services;
    }

    private static FakeMapsProvider ResolveProvider(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        MapsProvider provider = configuration
            .GetSection(MapsOptions.SectionName)
            .Get<MapsOptions>()?
            .Provider ?? MapsProvider.Fake;

        return provider switch
        {
            MapsProvider.Fake => serviceProvider.GetRequiredService<FakeMapsProvider>(),
            _ => throw new InvalidOperationException($"Maps provider '{provider}' is not supported."),
        };
    }
}
