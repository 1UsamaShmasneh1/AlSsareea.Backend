using AlSsareea.Modules.Pricing.Application;
using AlSsareea.Modules.Pricing.Contracts;
using AlSsareea.Modules.Pricing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Pricing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPricingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString("PricingDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:PricingDatabase is required.");
        services.AddDbContext<PricingDbContext>(options =>
            options.UseNpgsql(connection, npgsql =>
                npgsql.MigrationsAssembly(typeof(PricingDbContext).Assembly.FullName)
                    .MigrationsHistoryTable(PricingPersistence.MigrationsHistoryTable, PricingPersistence.Schema))
                .UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<PricingDbContext>("pricing-postgresql", tags: ["ready"]);
        services.AddScoped<IPricingPolicyRepository, PricingPolicyRepository>();
        services.AddScoped<PricingService>();
        services.AddScoped<IPricingService>(provider => provider.GetRequiredService<PricingService>());
        services.AddScoped<IPricingCalculator>(provider => provider.GetRequiredService<PricingService>());
        return services;
    }
}
