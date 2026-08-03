using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class OrdersPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task MigrationCreatesOwnedSchemaTablesAndHistory()
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString); await connection.OpenAsync();
        string[] expected = ["orders", "order_items", "order_item_options", "order_status_history", "order_creation_idempotency", "outbox_messages"];
        await using NpgsqlCommand tables = new("SELECT table_name FROM information_schema.tables WHERE table_schema = 'orders' ORDER BY table_name", connection);
        await using NpgsqlDataReader reader = await tables.ExecuteReaderAsync(); List<string> actual = []; while (await reader.ReadAsync()) actual.Add(reader.GetString(0)); await reader.CloseAsync();
        Assert.All(expected, x => Assert.Contains(x, actual));
        await using NpgsqlCommand history = new("SELECT COUNT(*) FROM orders.__ef_migrations_history", connection); Assert.True(Convert.ToInt64(await history.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) >= 1);
        await using NpgsqlCommand publicTables = new("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = ANY(ARRAY['orders','order_items','outbox_messages'])", connection); Assert.Equal(0L, Convert.ToInt64(await publicTables.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task MigrationUsesBigintSmallintNoCascadeAndNoCrossSchemaForeignKeys()
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString); await connection.OpenAsync();
        await using NpgsqlCommand types = new("SELECT data_type FROM information_schema.columns WHERE table_schema='orders' AND table_name='orders' AND column_name IN ('total_minor','status') ORDER BY column_name", connection);
        await using NpgsqlDataReader reader = await types.ExecuteReaderAsync(); List<string> values = []; while (await reader.ReadAsync()) values.Add(reader.GetString(0)); await reader.CloseAsync(); Assert.Contains("bigint", values); Assert.Contains("smallint", values);
        const string sql = "SELECT COUNT(*) FROM information_schema.referential_constraints rc JOIN information_schema.table_constraints tc ON tc.constraint_name=rc.constraint_name AND tc.constraint_schema=rc.constraint_schema JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name=rc.unique_constraint_name AND ccu.constraint_schema=rc.unique_constraint_schema WHERE tc.constraint_schema='orders' AND (rc.delete_rule='CASCADE' OR ccu.table_schema <> 'orders')";
        await using NpgsqlCommand constraints = new(sql, connection); Assert.Equal(0L, Convert.ToInt64(await constraints.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task OrderSnapshotHistoryIdempotencyAndOutboxPersistAtomically()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repository = new(db, new FixedClock(Now));
        Order order = CreateOrder(); OrderCreatedIntegrationEvent integrationEvent = Event(order);
        Assert.Equal(OrderCreatePersistenceResult.Created, await repository.CreateAsync(order, order.CustomerId, "order.create", Hash('a'), Hash('b'), [integrationEvent], default));
        OrderDetailsResponse? details = await repository.GetDetailsAsync(order.Id.Value, order.CustomerId, default); Assert.NotNull(details); Assert.Single(details.Items); Assert.Single(details.Items[0].Options); Assert.Single(details.Timeline); Assert.Equal("Falafel", details.Items[0].ProductName);
        Assert.Null(await repository.GetDetailsAsync(order.Id.Value, Guid.NewGuid(), default));
        Assert.Equal(1, await db.OutboxMessages.AsNoTracking().CountAsync(x => x.Id == new OrderOutboxMessageId(integrationEvent.Id)));
        Assert.Equal(1, await db.IdempotencyRecords.AsNoTracking().CountAsync(x => x.OrderId == order.Id));
    }

    [Fact]
    public async Task OutboxPayloadConstraintRequiresAJsonObject()
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        const string constraintSql = "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_order_outbox_payload'";
        await using NpgsqlCommand constraint = new(constraintSql, connection);
        string definition = Assert.IsType<string>(await constraint.ExecuteScalarAsync());
        Assert.Contains("jsonb_typeof(payload) = 'object'", definition, StringComparison.Ordinal);

        const string insertSql = "INSERT INTO orders.outbox_messages (id, event_type, payload, occurred_at_utc, created_at_utc, attempt_count) VALUES (@id, 'InvalidPayload', '[]'::jsonb, @now, @now, 0)";
        await using NpgsqlCommand insert = new(insertSql, connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("now", Now);
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(async () => await insert.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_order_outbox_payload", exception.ConstraintName);
    }

    [Fact]
    public async Task SameIdempotencyKeyCreatesOneOrderAndPayloadMismatchConflicts()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repository = new(db, new FixedClock(Now)); Guid customerId = Guid.NewGuid(); string key = Hash(Guid.NewGuid().ToString());
        Order first = CreateOrder(customerId); Assert.Equal(OrderCreatePersistenceResult.Created, await repository.CreateAsync(first, customerId, "order.create", key, Hash("same"), [Event(first)], default));
        Order retry = CreateOrder(customerId); Assert.Equal(OrderCreatePersistenceResult.DuplicateSameRequest, await repository.CreateAsync(retry, customerId, "order.create", key, Hash("same"), [Event(retry)], default));
        Order mismatch = CreateOrder(customerId); Assert.Equal(OrderCreatePersistenceResult.DuplicateDifferentRequest, await repository.CreateAsync(mismatch, customerId, "order.create", key, Hash("different"), [Event(mismatch)], default));
        Assert.Equal(1, await db.Orders.AsNoTracking().CountAsync(x => x.CustomerId == customerId));
    }

    [Fact]
    public async Task ConcurrentContextsDetectOptimisticConcurrencyConflict()
    {
        Order seeded = CreateOrder();
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope()) { OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repo = new(db, new FixedClock(Now)); Assert.Equal(OrderCreatePersistenceResult.Created, await repo.CreateAsync(seeded, seeded.CustomerId, "order.create", Hash(Guid.NewGuid().ToString()), Hash("seed"), [Event(seeded)], default)); }
        DbContextOptions<OrdersDbContext> options = new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using OrdersDbContext firstDb = new(options); await using OrdersDbContext secondDb = new(options); OrderRepository firstRepo = new(firstDb, new FixedClock(Now)); OrderRepository secondRepo = new(secondDb, new FixedClock(Now));
        Order first = (await firstRepo.GetForUpdateAsync(seeded.Id, default))!; Order second = (await secondRepo.GetForUpdateAsync(seeded.Id, default))!; first.Cancel(CancellationActor.Customer, "changed", null, Now.AddMinutes(1), Guid.NewGuid(), null); second.Cancel(CancellationActor.Customer, "changed", null, Now.AddMinutes(1), Guid.NewGuid(), null);
        Assert.True(await firstRepo.SaveAsync([], default)); Assert.False(await secondRepo.SaveAsync([], default));
    }

    [Fact]
    public async Task HasNoPendingModelChanges()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); Assert.False(db.Database.HasPendingModelChanges());
    }

    private static Order CreateOrder(Guid? customerId = null)
    {
        Guid customer = customerId ?? Guid.NewGuid(); Guid merchant = Guid.NewGuid(); Guid branch = Guid.NewGuid(); DateTime now = Now;
        OrderItemInput item = new(Guid.NewGuid(), 2, null, "Falafel", null, "SKU-1", 2, 450, 50, 0, 500, 1000, 0, 1000, null, [new(Guid.NewGuid(), Guid.NewGuid(), "Extras", "Tahini", 1, 50, 50)]);
        return Order.Create(OrderId.New(), Guid.NewGuid().ToString("N").ToUpperInvariant(), customer, merchant, branch, Guid.NewGuid(), OrderType.Restaurant, new(1000, 100, 0, 0, 0, 100, 50, 25, 0, 25, 1200, "ILS", "policy:1", now), new(customer, "Customer", null, "ar"), new(Guid.NewGuid(), "Home", "City", "Area", "Street", "1", null, null, null, 31.5, 35.0, null, "Street, City"), new(merchant, branch, "Merchant", "Branch", "Branch street", null), [item], null, null, now);
    }
    private static OrderCreatedIntegrationEvent Event(Order x) => new(Guid.NewGuid(), 1, x.Id.Value, x.OrderNumber, x.CustomerId, x.MerchantId, x.MerchantBranchId, x.SourceCartId, (short)x.Status, x.TotalMinor, x.Currency, Now);
    private static string Hash(char value) => new(value, 64);
    private static string Hash(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }
}
