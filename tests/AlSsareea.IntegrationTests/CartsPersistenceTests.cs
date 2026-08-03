using AlSsareea.Modules.Carts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class CartsPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task MigrationCreatesModuleTables()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CartsDbContext db = scope.ServiceProvider.GetRequiredService<CartsDbContext>();
        string[] tables = await db.Database.SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'carts'").ToArrayAsync();
        Assert.Contains("carts", tables); Assert.Contains("cart_items", tables); Assert.Contains("cart_item_options", tables); Assert.Contains("cart_idempotency_records", tables);
    }
}

