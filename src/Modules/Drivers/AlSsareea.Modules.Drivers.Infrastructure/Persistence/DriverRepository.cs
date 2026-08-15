using System.Text.Json;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Drivers.Infrastructure.Persistence;

internal sealed class DriverRepository(DriversDbContext db) : IDriverRepository
{
    public Task<Driver?> GetAsync(DriverId id, CancellationToken ct) => Aggregate().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Driver?> GetByUserAsync(Guid userId, CancellationToken ct) => Aggregate().SingleOrDefaultAsync(x => x.UserId == userId, ct);
    public Task<bool> UserHasDriverAsync(Guid userId, CancellationToken ct) => db.Drivers.AnyAsync(x => x.UserId == userId, ct);
    public async Task AddAsync(Driver driver, CancellationToken ct) => await db.Drivers.AddAsync(driver, ct);

    public async Task<DriverIdempotencyResult> CheckIdempotencyAsync(Guid actorUserId, string operation, string keyHash, string requestHash, CancellationToken ct)
    {
        var existing = await db.IdempotencyRecords.AsNoTracking()
            .Where(x => x.ActorUserId == actorUserId && x.Operation == operation && x.KeyHash == keyHash)
            .Select(x => new { x.RequestHash, x.ResponseStatus, x.ResponseJson })
            .SingleOrDefaultAsync(ct);
        if (existing is null) return new DriverIdempotencyResult(DriverIdempotencyState.New);
        return existing.RequestHash == requestHash
            ? new DriverIdempotencyResult(DriverIdempotencyState.SameRequest, existing.ResponseStatus, existing.ResponseJson)
            : new DriverIdempotencyResult(DriverIdempotencyState.DifferentRequest);
    }

    public async Task<bool> SaveOperationAsync(Driver driver, DriverIdempotencyEntry? idempotency, DriverAuditEntry? audit, IReadOnlyCollection<IIntegrationEvent> integrationEvents, CancellationToken ct)
    {
        if (idempotency is not null) db.IdempotencyRecords.Add(DriverIdempotencyRecord.Create(idempotency));
        if (audit is not null) db.AuditRecords.Add(DriverAuditRecord.Create(audit));
        DateTime createdAtUtc = audit?.OccurredAtUtc ?? idempotency?.CreatedAtUtc ?? throw new InvalidOperationException("Operation persistence requires audit or idempotency metadata.");
        foreach (IIntegrationEvent integrationEvent in integrationEvents) db.OutboxMessages.Add(DriverOutboxMessage.Create(new DriverOutboxMessageId(integrationEvent.Id), integrationEvent.GetType().FullName ?? integrationEvent.GetType().Name, JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()), integrationEvent.OccurredAtUtc, createdAtUtc));
        try { await db.SaveChangesAsync(ct); driver.ClearDomainEvents(); return true; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
    }

    public async Task<DriverProfileResponse?> GetProfileAsync(DriverId id, CancellationToken ct) => Map(await ProfileQuery().SingleOrDefaultAsync(x => x.Id == id, ct));
    public async Task<DriverProfileResponse?> GetProfileByUserAsync(Guid userId, CancellationToken ct) => Map(await ProfileQuery().SingleOrDefaultAsync(x => x.UserId == userId, ct));

    public async Task<PagedDriversResponse> ListAsync(DriverQuery query, CancellationToken ct)
    {
        int page = Math.Max(1, query.Page); int pageSize = Math.Clamp(query.PageSize, 1, DriverRules.MaximumPageSize); IQueryable<Driver> source = db.Drivers.AsNoTracking();
        if (query.Status.HasValue) source = source.Where(x => (short)x.Status == query.Status);
        if (query.ActivationStatus.HasValue) source = source.Where(x => (short)x.ActivationStatus == query.ActivationStatus);
        if (query.AvailabilityStatus.HasValue) source = source.Where(x => (short)x.AvailabilityStatus == query.AvailabilityStatus);
        if (query.EmploymentType.HasValue) source = source.Where(x => (short)x.EmploymentType == query.EmploymentType);
        if (query.ZoneId.HasValue) source = source.Where(x => x.ZoneAssignments.Any(z => z.ZoneId == query.ZoneId && z.IsActive));
        if (!string.IsNullOrWhiteSpace(query.Search)) { string search = query.Search.Trim(); source = source.Where(x => EF.Functions.ILike(x.DisplayName, $"%{search}%")); }
        int total = await source.CountAsync(ct);
        List<DriverSummaryResponse> items = await source.OrderByDescending(x => x.UpdatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new DriverSummaryResponse(x.Id.Value, x.DisplayName, (short)x.Status, (short)x.ActivationStatus, (short)x.EmploymentType, (short)x.AvailabilityStatus, x.CurrentLoad, x.MaximumConcurrentDeliveries, x.UpdatedAtUtc)).ToListAsync(ct);
        return new PagedDriversResponse(items, page, pageSize, total);
    }

    private IQueryable<Driver> Aggregate() => db.Drivers.Include(x => x.Vehicles).Include(x => x.Documents).Include(x => x.ZoneAssignments).Include(x => x.Shifts).Include(x => x.Violations).Include(x => x.Suspensions).AsSplitQuery();
    private IQueryable<Driver> ProfileQuery() => Aggregate().AsNoTracking();

    internal static DriverProfileResponse? Map(Driver? driver)
    {
        if (driver is null) return null;
        return new DriverProfileResponse(driver.Id.Value, driver.UserId, driver.DisplayName, driver.DateOfBirth, driver.ProfilePhotoMediaId, (short)driver.Status, (short)driver.ActivationStatus, (short)driver.EmploymentType, Availability(driver), driver.CreatedAtUtc, driver.UpdatedAtUtc, driver.ActivatedAtUtc, driver.ConcurrencyStamp,
            driver.Vehicles.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreatedAtUtc).Select(Vehicle).ToList(), driver.Documents.OrderByDescending(x => x.SubmittedAtUtc).Select(Document).ToList(), driver.ZoneAssignments.OrderByDescending(x => x.IsPrimary).ThenByDescending(x => x.AssignedAtUtc).Select(Zone).ToList());
    }
    internal static DriverAvailabilityResponse Availability(Driver x) => new((short)x.AvailabilityStatus, x.CurrentLoad, x.MaximumConcurrentDeliveries, x.LastAvailabilityChangedAtUtc);
    internal static VehicleResponse Vehicle(Vehicle x) => new(x.Id.Value, (short)x.Type, x.Make, x.Model, x.Year, x.Color, x.PlateNumber, x.RegistrationCountry, x.IsPrimary, (short)x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.VerifiedAtUtc, x.ConcurrencyStamp);
    internal static DriverDocumentResponse Document(DriverDocument x) => new(x.Id.Value, (short)x.Type, x.MediaAssetId, (short)x.Status, x.IssuedAtUtc, x.ExpiresAtUtc, x.SubmittedAtUtc, x.ReviewedAtUtc, x.RejectionReason, x.ConcurrencyStamp);
    internal static DriverZoneResponse Zone(DriverZoneAssignment x) => new(x.Id.Value, x.ZoneId, x.IsPrimary, x.IsActive, x.AssignedAtUtc, x.RemovedAtUtc);
    internal static DriverShiftResponse Shift(DriverShift x) => new(x.Id.Value, x.ScheduledStartUtc, x.ScheduledEndUtc, x.ActualStartUtc, x.ActualEndUtc, (short)x.Status, x.ConcurrencyStamp);
    internal static DriverViolationResponse Violation(DriverViolation x) => new(x.Id.Value, x.ViolationType, (short)x.Severity, x.Description, x.OccurredAtUtc, x.RecordedAtUtc, (short)x.Status, x.ResolvedAtUtc, x.ResolutionNotes);
    internal static DriverSuspensionResponse Suspension(DriverSuspension x) => new(x.Id.Value, x.ReasonCode, x.Reason, x.StartsAtUtc, x.EndsAtUtc, (short)x.Status, x.LiftedAtUtc, x.LiftReason);
}
