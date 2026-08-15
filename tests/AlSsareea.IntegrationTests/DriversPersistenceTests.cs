using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.IntegrationTests;

[Collection(PostgresTestSuite.Name)]
public sealed class DriversPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task MigrationCreatesOwnedSchemaTablesAndHistoryWithoutPendingChanges()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); Assert.False(db.Database.HasPendingModelChanges());
        await using var command = db.Database.GetDbConnection().CreateCommand(); await db.Database.OpenConnectionAsync(); command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'drivers' ORDER BY table_name"; await using var reader = await command.ExecuteReaderAsync(); List<string> tables = []; while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        string[] expected = ["__ef_migrations_history", "audit_records", "driver_documents", "driver_shifts", "driver_suspensions", "driver_violations", "driver_zone_assignments", "drivers", "idempotency_records", "outbox_messages", "vehicles"]; Assert.All(expected, x => Assert.Contains(x, tables)); Assert.DoesNotContain("driver_locations", tables); Assert.DoesNotContain("driver_assignments", tables); Assert.DoesNotContain("earnings", tables);
    }

    [Fact]
    public async Task DriverPersistsAndUserIdIsUnique()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); Guid userId = Guid.NewGuid(); DateTime now = DateTime.UtcNow; db.Drivers.Add(Driver.Create(DriverId.New(), userId, "Persistence Driver", EmploymentType.Employee, 2, null, now)); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); Driver saved = await db.Drivers.SingleAsync(x => x.UserId == userId); Assert.Equal("Persistence Driver", saved.DisplayName); db.Drivers.Add(Driver.Create(DriverId.New(), userId, "Duplicate Driver", EmploymentType.Employee, 2, null, now)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task OutboxPayloadConstraintMatchesOrdersAndRequiresJsonObject()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>();
        await using var definitionCommand = db.Database.GetDbConnection().CreateCommand(); await db.Database.OpenConnectionAsync(); definitionCommand.CommandText = "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_driver_outbox_payload' AND connamespace = 'drivers'::regnamespace"; string definition = (string)(await definitionCommand.ExecuteScalarAsync())!; Assert.Contains("jsonb_typeof(payload) = 'object'", definition, StringComparison.Ordinal);

        DateTime now = DateTime.UtcNow; await using var insert = db.Database.GetDbConnection().CreateCommand(); insert.CommandText = "INSERT INTO drivers.outbox_messages (id, event_type, payload, occurred_at_utc, created_at_utc, attempt_count) VALUES (@id, 'InvalidPayload', '[]'::jsonb, @now, @now, 0)"; var id = insert.CreateParameter(); id.ParameterName = "id"; id.Value = Guid.NewGuid(); insert.Parameters.Add(id); var at = insert.CreateParameter(); at.ParameterName = "now"; at.Value = now; insert.Parameters.Add(at); await Assert.ThrowsAnyAsync<Exception>(() => insert.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task AuditFailureRollsBackBusinessStateIdempotencyAndOutbox()
    {
        (DriverId driverId, string originalName) = await PersistDriver(); Guid actor = Guid.NewGuid(); Guid eventId = Guid.NewGuid(); DateTime now = DateTime.UtcNow;
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); DriverRepository repository = new(db); Driver driver = (await repository.GetAsync(driverId, default))!; driver.UpdateProfile("Should Roll Back", null, null, now);
            DriverIdempotencyEntry idem = new(actor, "profile.update", new string('a', 64), new string('b', 64), driver.Id, DriverOperationStatus.Success, "{}", now);
            DriverAuditEntry invalidAudit = new(actor, driver.Id, new string('x', 101), now, null, null);
            Assert.False(await repository.SaveOperationAsync(driver, idem, invalidAudit, [new TestDriverEvent(eventId, now)], default));
        }
        await AssertOperationRolledBack(driverId, originalName, actor, eventId);
    }

    [Fact]
    public async Task OutboxFailureRollsBackBusinessStateAuditAndIdempotency()
    {
        (DriverId driverId, string originalName) = await PersistDriver(); Guid actor = Guid.NewGuid(); Guid eventId = Guid.NewGuid(); DateTime now = DateTime.UtcNow;
        await using (AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope())
        {
            DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); DriverRepository repository = new(db); Driver driver = (await repository.GetAsync(driverId, default))!; driver.UpdateProfile("Should Also Roll Back", null, null, now);
            db.OutboxMessages.Add(DriverOutboxMessage.Create(new DriverOutboxMessageId(Guid.NewGuid()), "InvalidPayload", "[]", now, now));
            DriverIdempotencyEntry idem = new(actor, "profile.update", new string('c', 64), new string('d', 64), driver.Id, DriverOperationStatus.Success, "{}", now);
            DriverAuditEntry audit = new(actor, driver.Id, "profile.update", now, null, null);
            Assert.False(await repository.SaveOperationAsync(driver, idem, audit, [new TestDriverEvent(eventId, now)], default));
        }
        await AssertOperationRolledBack(driverId, originalName, actor, eventId);
    }

    private async Task<(DriverId Id, string Name)> PersistDriver()
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>(); Driver driver = Driver.Create(DriverId.New(), Guid.NewGuid(), "Atomic Driver", EmploymentType.Employee, 2, null, DateTime.UtcNow); db.Drivers.Add(driver); await db.SaveChangesAsync(); return (driver.Id, driver.DisplayName);
    }

    private async Task AssertOperationRolledBack(DriverId driverId, string expectedName, Guid actor, Guid eventId)
    {
        await using AsyncServiceScope scope = fixture.ApiFactory.Services.CreateAsyncScope(); DriversDbContext db = scope.ServiceProvider.GetRequiredService<DriversDbContext>();
        Assert.Equal(expectedName, (await db.Drivers.AsNoTracking().SingleAsync(x => x.Id == driverId)).DisplayName); Assert.False(await db.IdempotencyRecords.AnyAsync(x => x.ActorUserId == actor)); Assert.False(await db.AuditRecords.AnyAsync(x => x.ActorUserId == actor)); Assert.False(await db.OutboxMessages.AnyAsync(x => x.Id == new DriverOutboxMessageId(eventId)));
    }

    private sealed record TestDriverEvent(Guid Id, DateTime OccurredAtUtc) : IIntegrationEvent;
}
