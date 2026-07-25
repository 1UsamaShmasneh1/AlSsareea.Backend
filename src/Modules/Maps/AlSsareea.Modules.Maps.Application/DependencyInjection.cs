using AlSsareea.Modules.Maps.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Maps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMapsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IMapsModule, MapsModule>();
        return services;
    }
}
