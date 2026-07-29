using AlSsareea.Modules.Promotions.Application;
using AlSsareea.Modules.Promotions.Contracts;
using AlSsareea.Modules.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Promotions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPromotionsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString("PromotionsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("MerchantsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("MapsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:PromotionsDatabase is required.");
        services.AddDbContext<PromotionsDbContext>(options => options
            .UseNpgsql(connection, npgsql => npgsql
                .MigrationsAssembly(typeof(PromotionsDbContext).Assembly.FullName)
                .MigrationsHistoryTable(PromotionsPersistence.MigrationsHistoryTable, PromotionsPersistence.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<PromotionsDbContext>("promotions-postgresql", tags: ["ready"]);
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IPromotionScopeAuthorizer, PromotionScopeAuthorizer>();
        services.AddScoped<PromotionsService>();
        services.AddScoped<IPromotionsService>(provider => provider.GetRequiredService<PromotionsService>());
        services.AddScoped<ICartPromotionEvaluator>(provider => provider.GetRequiredService<PromotionsService>());
        return services;
    }
}
