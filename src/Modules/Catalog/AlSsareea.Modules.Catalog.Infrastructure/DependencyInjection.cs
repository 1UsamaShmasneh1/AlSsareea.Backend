using AlSsareea.Modules.Catalog.Application;
using AlSsareea.Modules.Catalog.Contracts;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString("CatalogDatabase") ?? configuration.GetConnectionString("MerchantsDatabase"); if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:CatalogDatabase is required.");
        services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName).MigrationsHistoryTable(CatalogPersistence.MigrationsHistoryTable, CatalogPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("catalog-postgresql", tags: ["ready"]); services.AddScoped<ICatalogRepository, CatalogRepository>(); services.AddScoped<IProductRepository, ProductRepository>(); services.AddScoped<CatalogService>(); services.AddScoped<ICatalogService>(p => p.GetRequiredService<CatalogService>()); services.AddScoped<IProductSnapshotProvider>(p => p.GetRequiredService<CatalogService>()); return services;
    }
}
