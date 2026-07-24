using System.Data.Common;
using AlSsareea.Modules.Customers.Domain;
using AlSsareea.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class CustomersPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesCustomersSchemaTablesIndexesAndNoIdentityForeignKey()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CustomersDbContext db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        await db.Database.OpenConnectionAsync();
        DbConnection connection = db.Database.GetDbConnection();

        Assert.Equal(4, await ScalarAsync<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'customers' AND table_name IN ('customers','customer_addresses','customer_preferences','customer_status_history')"));
        Assert.Equal(0, await ScalarAsync<long>(connection, "SELECT count(*) FROM information_schema.table_constraints tc JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name=tc.constraint_name AND ccu.constraint_schema=tc.constraint_schema WHERE tc.constraint_schema='customers' AND tc.constraint_type='FOREIGN KEY' AND ccu.table_schema='identity'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname='customers' AND indexname='ux_customer_addresses_active_default' AND indexdef ILIKE '%WHERE%is_default%deleted_at_utc%'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_indexes WHERE schemaname='customers' AND indexname='ix_customer_addresses_location_gist' AND indexdef ILIKE '%gist%'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT count(*) FROM information_schema.tables WHERE table_schema='customers' AND table_name='__ef_migrations_history'"));
    }

    [Fact]
    public async Task AggregateRoundTripPersistsPreferencesAddressPointAndSrid()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CustomersDbContext db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Customer customer = Create();
        customer.AddAddress(CustomerAddressId.New(), "Home", CustomerAddressType.Home, "Jerusalem", "Center", "Main", "12A", "2", "4", "91000", "provider-neutral", new GeoCoordinate(31.778, 35.235), "Call on arrival", false, Now.AddMinutes(1), customer.UserId);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Customer loaded = await db.Customers.Include(x => x.Addresses).Include(x => x.Preferences).SingleAsync(x => x.Id == customer.Id);
        CustomerAddress address = Assert.Single(loaded.Addresses);
        Assert.True(address.IsDefault);
        Assert.Equal(31.778, address.Location?.Latitude);
        Assert.Equal(35.235, address.Location?.Longitude);
        Assert.Equal("ar", loaded.Preferences.PreferredLanguage);

        await db.Database.OpenConnectionAsync();
        DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT ST_SRID(location) FROM customers.customer_addresses WHERE id = @id";
        DbParameter parameter = command.CreateParameter(); parameter.ParameterName = "id"; parameter.Value = address.Id.Value; command.Parameters.Add(parameter);
        Assert.Equal(4326, Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task UniqueUserIdAndDefaultAddressConstraintsAreDatabaseSafeguards()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CustomersDbContext db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Guid userId = Guid.NewGuid();
        db.Customers.Add(Customer.Create(CustomerId.New(), userId, "One", "User", null, Now, userId));
        db.Customers.Add(Customer.Create(CustomerId.New(), userId, "Two", "User", null, Now, userId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AddressSoftDeleteIsHiddenByNormalQueries()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CustomersDbContext db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Customer customer = Create();
        CustomerAddress address = customer.AddAddress(CustomerAddressId.New(), "Home", CustomerAddressType.Home, "City", null, "Street", null, null, null, null, null, null, null, false, Now.AddMinutes(1), customer.UserId);
        db.Customers.Add(customer); await db.SaveChangesAsync();
        customer.DeleteAddress(address.Id, Now.AddMinutes(2), customer.UserId); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Empty(await db.CustomerAddresses.Where(x => x.CustomerId == customer.Id).ToListAsync());
        Assert.Single(await db.CustomerAddresses.IgnoreQueryFilters().Where(x => x.CustomerId == customer.Id).ToListAsync());
    }

    [Fact]
    public async Task CustomerConcurrencyStampCausesRealConflict()
    {
        Customer customer;
        await using (AsyncServiceScope setup = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            CustomersDbContext db = setup.ServiceProvider.GetRequiredService<CustomersDbContext>();
            customer = Create(); db.Customers.Add(customer); await db.SaveChangesAsync();
        }
        await using AsyncServiceScope firstScope = fixture.ApiFactory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = fixture.ApiFactory.Services.CreateAsyncScope();
        CustomersDbContext first = firstScope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        CustomersDbContext second = secondScope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Customer firstCopy = await first.Customers.SingleAsync(x => x.Id == customer.Id);
        Customer secondCopy = await second.Customers.SingleAsync(x => x.Id == customer.Id);
        firstCopy.UpdateProfile("First", "Winner", null, Now.AddMinutes(3), customer.UserId); await first.SaveChangesAsync();
        secondCopy.UpdateProfile("Second", "Loser", null, Now.AddMinutes(4), customer.UserId);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task StatusAndHistoryPersistAtomicallyAndHistoryIsAppendOnly()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        CustomersDbContext db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Customer customer = Create(); customer.Block("fraud review", Now.AddMinutes(1), customer.UserId, "test-correlation");
        db.Customers.Add(customer); await db.SaveChangesAsync();
        CustomerStatusHistory history = await db.CustomerStatusHistory.SingleAsync(x => x.CustomerId == customer.Id);
        db.Entry(history).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CurrentCustomerEndpointRequiresAuthentication()
    {
        HttpClient client = fixture.ApiFactory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/v1/customers/me");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Customer Create()
    {
        Guid user = Guid.NewGuid();
        return Customer.Create(CustomerId.New(), user, "Test", "Customer", null, Now, user);
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
