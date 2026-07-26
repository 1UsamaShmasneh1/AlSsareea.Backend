using AlSsareea.Modules.Merchants.Application;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Merchants.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMerchantsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("MerchantsDatabase");
        if (string.IsNullOrWhiteSpace(connectionString)) connectionString = configuration.GetConnectionString("MapsDatabase");
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("ConnectionStrings:MerchantsDatabase is required.");
        services.AddDbContext<MerchantsDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()
                .MigrationsAssembly(typeof(MerchantsDbContext).Assembly.FullName)
                .MigrationsHistoryTable(MerchantsPersistence.MigrationsHistoryTable, MerchantsPersistence.Schema))
            .UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<MerchantsDbContext>("merchants-postgresql", tags: ["ready"]);
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<IMerchantBranchRepository, MerchantBranchRepository>();
        services.AddScoped<IMerchantEmployeeRepository, MerchantEmployeeRepository>();
        services.AddScoped<IMerchantsService, MerchantsService>();
        services.AddScoped<IMerchantCatalogScopeProvider, MerchantCatalogScopeProvider>();
        return services;
    }
}
