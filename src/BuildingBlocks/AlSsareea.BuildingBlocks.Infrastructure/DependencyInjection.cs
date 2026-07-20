using AlSsareea.BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
