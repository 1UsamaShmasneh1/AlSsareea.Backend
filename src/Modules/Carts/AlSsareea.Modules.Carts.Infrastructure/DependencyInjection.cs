using AlSsareea.Modules.Carts.Application;
using AlSsareea.Modules.Carts.Contracts;
using AlSsareea.Modules.Carts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Carts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCartsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CartsOptions>().Bind(configuration.GetSection(CartsOptions.SectionName)).Validate(x => x.ActiveCartLifetime > TimeSpan.Zero && x.MaximumItems is > 0 and <= 500 && x.MaximumQuantityPerItem is > 0 and <= 999 && x.MaximumIdempotencyKeyLength is > 0 and <= 500, "Carts configuration is invalid.").ValidateOnStart();
        string? connection = configuration.GetConnectionString("CartsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("PromotionsDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:CartsDatabase is required.");
        services.AddDbContext<CartsDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(CartsDbContext).Assembly.FullName).MigrationsHistoryTable(CartsPersistence.MigrationsHistoryTable, CartsPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<CartsDbContext>("carts-postgresql", tags: ["ready"]);
        services.AddScoped<ICartRepository, CartRepository>(); services.AddScoped<ICartService, CartService>(); services.AddScoped<IOrderCheckoutProvider, OrderCheckoutProvider>(); return services;
    }
}
