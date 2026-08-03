using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Orders.Application;
using AlSsareea.Modules.Orders.Contracts;
using AlSsareea.Modules.Orders.Domain;
using AlSsareea.Modules.Orders.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class MerchantOrderOperationsPersistenceTests(PostgresFixture fixture)
{
    private static readonly DateTime Now = new(2026, 8, 3, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcceptPersistsPreparationAuditIdempotencyHistoryAndOutboxAtomically()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope();
        OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repository = new(db, new FixedClock(Now));
        Order order = SubmittedOrder(); await Seed(repository, order);
        Guid actor = Guid.NewGuid(); OrderStatus previous = order.Status; order.AcceptByMerchant(25, Now.AddMinutes(3), actor, "corr-accept");
        MerchantOrderChangedIntegrationEvent integration = Changed(order, actor, previous, "merchant.accept");
        MerchantOrderAuditEntry audit = Audit(order, actor, previous, "merchant.accept", Hash('a'));
        Assert.Equal(MerchantOrderPersistenceResult.Saved, await repository.SaveMerchantOperationAsync(order, actor, "merchant.accept", Hash('a'), Hash('b'), audit, [integration], default));

        db.ChangeTracker.Clear(); Order persisted = await db.Orders.SingleAsync(x => x.Id == order.Id);
        Assert.Equal(OrderStatus.AcceptedByMerchant, persisted.Status); Assert.Equal(25, persisted.EstimatedPreparationMinutes); Assert.Equal(Now.AddMinutes(28), persisted.EstimatedReadyAtUtc);
        Assert.Equal(1, await db.MerchantOrderAudit.CountAsync(x => x.OrderId == order.Id));
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync(x => x.OrderId == order.Id && x.Operation == "merchant.accept"));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.Id == new OrderOutboxMessageId(integration.Id)));
        Assert.Equal(4, await db.OrderStatusHistory.CountAsync(x => x.OrderId == order.Id));
    }

    [Fact]
    public async Task MerchantOperationIdempotencyRejectsPayloadMismatchWithoutDuplicateAuditOrOutbox()
    {
        Guid actor = Guid.NewGuid(); string key = Hash('c'); Order order = SubmittedOrder();
        await using (AsyncServiceScope seedScope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            OrdersDbContext db = seedScope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repo = new(db, new FixedClock(Now)); await Seed(repo, order);
            OrderStatus previous = order.Status; order.AcceptByMerchant(20, Now.AddMinutes(3), actor);
            Assert.Equal(MerchantOrderPersistenceResult.Saved, await repo.SaveMerchantOperationAsync(order, actor, "merchant.accept", key, Hash('d'), Audit(order, actor, previous, "merchant.accept", key), [Changed(order, actor, previous, "merchant.accept")], default));
        }
        await using AsyncServiceScope retryScope = fixture.ApiFactory.Services.CreateAsyncScope(); OrdersDbContext retryDb = retryScope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository retry = new(retryDb, new FixedClock(Now)); Order loaded = (await retry.GetForUpdateAsync(order.Id, default))!;
        MerchantOrderAuditEntry audit = Audit(loaded, actor, loaded.Status, "merchant.accept", key);
        Assert.Equal(MerchantOrderPersistenceResult.DuplicateSameRequest, await retry.SaveMerchantOperationAsync(loaded, actor, "merchant.accept", key, Hash('d'), audit, [Changed(loaded, actor, loaded.Status, "merchant.accept")], default));
        Assert.Equal(MerchantOrderPersistenceResult.DuplicateDifferentRequest, await retry.SaveMerchantOperationAsync(loaded, actor, "merchant.accept", key, Hash('e'), audit, [Changed(loaded, actor, loaded.Status, "merchant.accept")], default));
        Assert.Equal(1, await retryDb.MerchantOrderAudit.CountAsync(x => x.OrderId == order.Id));
        string[] payloads = await retryDb.OutboxMessages.Where(x => x.EventType == nameof(MerchantOrderChangedIntegrationEvent)).Select(x => x.Payload).ToArrayAsync();
        Assert.Equal(1, payloads.Count(x => x.Contains(order.Id.Value.ToString(), StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ConcurrentAcceptAndRejectCannotBothWin()
    {
        Order seeded = SubmittedOrder(); await using (AsyncServiceScope setup = fixture.ApiFactory.Services.CreateAsyncScope()) { OrdersDbContext db = setup.ServiceProvider.GetRequiredService<OrdersDbContext>(); await Seed(new OrderRepository(db, new FixedClock(Now)), seeded); }
        DbContextOptions<OrdersDbContext> options = new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using OrdersDbContext firstDb = new(options); await using OrdersDbContext secondDb = new(options); OrderRepository firstRepo = new(firstDb, new FixedClock(Now)); OrderRepository secondRepo = new(secondDb, new FixedClock(Now));
        Order first = (await firstRepo.GetForUpdateAsync(seeded.Id, default))!; Order second = (await secondRepo.GetForUpdateAsync(seeded.Id, default))!; Guid actor = Guid.NewGuid();
        OrderStatus previous = first.Status; first.AcceptByMerchant(30, Now.AddMinutes(3), actor); second.RejectByMerchant(MerchantOrderRejectionReason.CannotFulfill, null, Now.AddMinutes(3), actor);
        Assert.Equal(MerchantOrderPersistenceResult.Saved, await firstRepo.SaveMerchantOperationAsync(first, actor, "merchant.accept", Hash('f'), Hash('1'), Audit(first, actor, previous, "merchant.accept", Hash('f')), [Changed(first, actor, previous, "merchant.accept")], default));
        Assert.Equal(MerchantOrderPersistenceResult.ConcurrencyConflict, await secondRepo.SaveMerchantOperationAsync(second, actor, "merchant.reject", Hash('0'), Hash('2'), Audit(second, actor, previous, "merchant.reject", Hash('0')), [Changed(second, actor, previous, "merchant.reject")], default));
    }

    [Fact]
    public async Task MerchantReadProjectionEnforcesMerchantAndBranchIsolationWithFiltersAndPagination()
    {
        Order first = SubmittedOrder(); Order second = SubmittedOrder();
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repository = new(db, new FixedClock(Now)); await Seed(repository, first); await Seed(repository, second);
        MerchantOrderOperationsScope allowed = new(first.MerchantId, true, first.MerchantBranchId, false);
        MerchantOrderQueryParameters query = new() { Bucket = "new", BranchId = first.MerchantBranchId, Status = (short)OrderStatus.Submitted, FromUtc = Now, ToUtc = Now.AddHours(1), Page = 1, PageSize = 1 };
        PagedMerchantOrdersResponse page = await repository.ListMerchantAsync([allowed], query, default);
        Assert.Single(page.Items); Assert.Equal(first.Id.Value, page.Items[0].OrderId); Assert.Equal(1, page.TotalCount);
        Assert.Null(await repository.GetMerchantDetailsAsync(second.Id.Value, [allowed], default));
        MerchantOrderDetails details = (await repository.GetMerchantDetailsAsync(first.Id.Value, [allowed], default))!;
        Assert.Equal("Customer", details.CustomerDisplayName); Assert.DoesNotContain("phone", System.Text.Json.JsonSerializer.Serialize(details), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditIsAppendOnly()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); OrdersDbContext db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>(); OrderRepository repo = new(db, new FixedClock(Now)); Order order = SubmittedOrder(); await Seed(repo, order); Guid actor = Guid.NewGuid(); OrderStatus previous = order.Status; order.AcceptByMerchant(10, Now.AddMinutes(3), actor);
        await repo.SaveMerchantOperationAsync(order, actor, "merchant.accept", Hash('3'), Hash('4'), Audit(order, actor, previous, "merchant.accept", Hash('3')), [Changed(order, actor, previous, "merchant.accept")], default);
        MerchantOrderAuditRecord record = await db.MerchantOrderAudit.SingleAsync(x => x.OrderId == order.Id); db.Remove(record);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static async Task Seed(OrderRepository repository, Order order) => Assert.Equal(OrderCreatePersistenceResult.Created, await repository.CreateAsync(order, order.CustomerId, "order.create", Hash(Guid.NewGuid().ToString()), Hash(Guid.NewGuid().ToString()), [], default));
    private static MerchantOrderAuditEntry Audit(Order order, Guid actor, OrderStatus previous, string operation, string key) => new(actor, order.MerchantId, order.MerchantBranchId, order.Id.Value, operation, previous, order.Status, Now.AddMinutes(3), "corr", key, null);
    private static MerchantOrderChangedIntegrationEvent Changed(Order order, Guid actor, OrderStatus previous, string operation) => new(Guid.NewGuid(), 1, order.Id.Value, order.OrderNumber, order.MerchantId, order.MerchantBranchId, operation, (short)previous, (short)order.Status, actor, order.EstimatedPreparationMinutes, order.EstimatedReadyAtUtc, Now.AddMinutes(3));
    private static Order SubmittedOrder()
    {
        Guid customer = Guid.NewGuid(); Guid merchant = Guid.NewGuid(); Guid branch = Guid.NewGuid(); OrderItemInput item = new(Guid.NewGuid(), 1, null, "Falafel", null, null, 1, 1000, 0, 0, 1000, 1000, 0, 1000, null, []);
        Order order = Order.Create(OrderId.New(), Guid.NewGuid().ToString("N").ToUpperInvariant(), customer, merchant, branch, Guid.NewGuid(), OrderType.Restaurant, new(1000, 0, 0, 0, 0, 100, 50, 25, 0, 25, 1200, "ILS", null, Now), new(customer, "Customer", null, "ar"), new(Guid.NewGuid(), "Home", "City", null, "Street", null, null, null, null, null, null, null, null), new(merchant, branch, "Merchant", "Branch", null, null), [item], null, null, Now);
        order.MarkPaymentAuthorized(Now.AddMinutes(1)); order.Submit(Now.AddMinutes(2)); return order;
    }
    private static string Hash(char value) => new(value, 64);
    private static string Hash(string value) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    private sealed class FixedClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }
}
