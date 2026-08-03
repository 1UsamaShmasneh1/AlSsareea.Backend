using AlSsareea.Modules.Carts.Infrastructure.Persistence;
using AlSsareea.Modules.Catalog.Infrastructure.Persistence;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Infrastructure.Persistence;
using AlSsareea.Modules.Media.Infrastructure.Persistence;
using AlSsareea.Modules.Merchants.Infrastructure.Persistence;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using AlSsareea.Modules.Pricing.Infrastructure.Persistence;
using AlSsareea.Modules.Promotions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AlSsareea.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(
        "postgis/postgis:17-3.5@sha256:404171ea9058c801f405af25d63b3b8e5c9e50f2759e49390dbcc3c7ee533f4d")
        .WithDatabase("alssareea_tests")
        .WithUsername("alssareea")
        .WithPassword("alssareea_test_password")
        .Build();

    public ApiFactory ApiFactory { get; private set; } = null!;
    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ApiFactory = new ApiFactory(_container.GetConnectionString());

        await using AsyncServiceScope scope = ApiFactory.Services.CreateAsyncScope();
        IdentityDbContext dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync();
        CustomersDbContext customersDbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        await customersDbContext.Database.MigrateAsync();
        MerchantsDbContext merchantsDbContext = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        await merchantsDbContext.Database.MigrateAsync();
        CatalogDbContext catalogDbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalogDbContext.Database.MigrateAsync();
        MediaDbContext mediaDbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        await mediaDbContext.Database.MigrateAsync();
        PricingDbContext pricingDbContext = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        await pricingDbContext.Database.MigrateAsync();
        PromotionsDbContext promotionsDbContext = scope.ServiceProvider.GetRequiredService<PromotionsDbContext>();
        await promotionsDbContext.Database.MigrateAsync();
        CartsDbContext cartsDbContext = scope.ServiceProvider.GetRequiredService<CartsDbContext>();
        await cartsDbContext.Database.MigrateAsync();
        OrdersDbContext ordersDbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await ordersDbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await ApiFactory.DisposeAsync();
        await _container.DisposeAsync();
    }
}
