using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Drivers.Application;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Drivers.Domain;
using AlSsareea.Modules.Drivers.Infrastructure.Persistence;
using AlSsareea.Modules.Identity.Contracts;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Media.Contracts;

namespace AlSsareea.Modules.Drivers.Infrastructure;

internal sealed class DriverService(IDriverRepository repository, IIdentityUserLookup identity, IMapsModule maps, IMediaAssetLookup media, IClock clock) : IDriverService, IDriverEligibilityProvider, IDriverOperationalSnapshotProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DriverOperationResult<DriverProfileResponse>> CreateAsync(DriverActor actor, CreateDriverRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (actor.UserId == Guid.Empty || !await identity.IsActiveUserAsync(actor.UserId, ct)) return Invalid<DriverProfileResponse>(DriverErrorCodes.IdentityInvalid);
        if (request.ProfilePhotoMediaId.HasValue && !await IsReadyMedia(request.ProfilePhotoMediaId.Value, ct)) return Invalid<DriverProfileResponse>(DriverErrorCodes.MediaInvalid);
        string? keyHash = KeyHash(idempotencyKey); if (keyHash is null) return Invalid<DriverProfileResponse>(); string requestHash = RequestHash(request);
        DriverIdempotencyResult idempotency = await repository.CheckIdempotencyAsync(actor.UserId, "create", keyHash, requestHash, ct);
        if (idempotency.State == DriverIdempotencyState.DifferentRequest) return Conflict<DriverProfileResponse>(DriverErrorCodes.IdempotencyConflict);
        if (idempotency.State == DriverIdempotencyState.SameRequest) return Replay<DriverProfileResponse>(idempotency);
        Driver? existing = await repository.GetByUserAsync(actor.UserId, ct);
        if (existing is not null)
        {
            DriverIdempotencyResult duplicate = await repository.CheckIdempotencyAsync(actor.UserId, "create", keyHash, requestHash, ct);
            return duplicate.State == DriverIdempotencyState.SameRequest ? Replay<DriverProfileResponse>(duplicate) : Conflict<DriverProfileResponse>();
        }
        try
        {
            DateTime now = clock.UtcNow; Driver driver = Driver.Create(DriverId.New(), actor.UserId, request.DisplayName, (EmploymentType)request.EmploymentType, request.MaximumConcurrentDeliveries, request.ProfilePhotoMediaId, now); await repository.AddAsync(driver, ct);
            DriverProfileResponse response = DriverRepository.Map(driver)!;
            DriverIdempotencyEntry idem = Idempotency(actor.UserId, "create", keyHash, requestHash, driver.Id, DriverOperationStatus.Created, response, now);
            if (!await repository.SaveOperationAsync(driver, idem, Audit(actor, driver, "DriverCreated", now, null), Events(driver, "create", now), ct))
            {
                DriverIdempotencyResult duplicate = await repository.CheckIdempotencyAsync(actor.UserId, "create", keyHash, requestHash, ct);
                return duplicate.State == DriverIdempotencyState.SameRequest ? Replay<DriverProfileResponse>(duplicate) : Conflict<DriverProfileResponse>(DriverErrorCodes.ConcurrencyConflict);
            }
            return new DriverOperationResult<DriverProfileResponse>(DriverOperationStatus.Created, response);
        }
        catch (DomainException) { return Invalid<DriverProfileResponse>(); }
    }

    public async Task<DriverOperationResult<DriverProfileResponse>> GetMyAsync(DriverActor actor, CancellationToken ct) => From(await repository.GetProfileByUserAsync(actor.UserId, ct));
    public async Task<DriverOperationResult<DriverProfileResponse>> GetAsync(DriverActor actor, Guid driverId, CancellationToken ct) => From(await SafeProfile(driverId, ct));
    public async Task<DriverOperationResult<PagedDriversResponse>> ListAsync(DriverQuery query, CancellationToken ct) => Success(await repository.ListAsync(query, ct));

    public async Task<DriverOperationResult<DriverProfileResponse>> UpdateProfileAsync(DriverActor actor, UpdateDriverProfileRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (request.ProfilePhotoMediaId.HasValue && !await IsReadyMedia(request.ProfilePhotoMediaId.Value, ct)) return Invalid<DriverProfileResponse>(DriverErrorCodes.MediaInvalid);
        return await ExecuteByUser(actor, "profile.update", idempotencyKey, request, request.ConcurrencyStamp, (driver, now) => driver.UpdateProfile(request.DisplayName, request.DateOfBirth, request.ProfilePhotoMediaId, now), driver => DriverRepository.Map(driver)!, null, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> TransitionAsync(DriverActor actor, Guid driverId, string operation, Guid concurrencyStamp, string? reason, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), operation, idempotencyKey, new { concurrencyStamp, reason }, concurrencyStamp, (driver, now) =>
    {
        switch (operation) { case "submit-review": driver.SubmitForReview(now); break; case "approve": driver.Approve(now); break; case "reject": driver.Reject(now); break; case "activate": driver.Activate(now); break; case "deactivate": driver.Deactivate(now); break; case "archive": driver.Archive(now); break; default: throw new DomainException("Driver transition is invalid."); }
    }, driver => DriverRepository.Map(driver)!, reason, ct);

    public async Task<DriverOperationResult<VehicleResponse>> AddVehicleAsync(DriverActor actor, AddVehicleRequest request, string idempotencyKey, CancellationToken ct)
    {
        Vehicle? created = null;
        return await ExecuteByUser(actor, "vehicle.add", idempotencyKey, request, null, (driver, now) => created = driver.AddVehicle((VehicleType)request.VehicleType, request.Make, request.Model, request.Year, request.Color, request.PlateNumber, request.RegistrationCountry, request.IsPrimary, now), driver => DriverRepository.Vehicle(created ?? driver.Vehicles.OrderByDescending(x => x.CreatedAtUtc).First()), null, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> ReviewVehicleAsync(DriverActor actor, Guid driverId, Guid vehicleId, bool approve, VehicleReviewRequest request, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), approve ? "vehicle.approve" : "vehicle.reject", idempotencyKey, request, request.ConcurrencyStamp, (driver, now) => { Vehicle vehicle = driver.Vehicle(new VehicleId(vehicleId)); if (vehicle.ConcurrencyStamp != request.ConcurrencyStamp) throw new ConcurrencyException(); if (approve) vehicle.Approve(now); else vehicle.Reject(now); }, driver => DriverRepository.Map(driver)!, request.Reason, ct);

    public Task<DriverOperationResult<DriverProfileResponse>> SetPrimaryVehicleAsync(DriverActor actor, Guid vehicleId, string idempotencyKey, CancellationToken ct) => ExecuteByUser(actor, "vehicle.set-primary", idempotencyKey, new { vehicleId }, null, (driver, now) => driver.SetPrimaryVehicle(new VehicleId(vehicleId), now), driver => DriverRepository.Map(driver)!, null, ct);

    public async Task<DriverOperationResult<DriverDocumentResponse>> SubmitDocumentAsync(DriverActor actor, SubmitDriverDocumentRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (!await IsReadyMedia(request.MediaAssetId, ct)) return Invalid<DriverDocumentResponse>(DriverErrorCodes.MediaInvalid); DriverDocument? created = null;
        return await ExecuteByUser(actor, "document.submit", idempotencyKey, request, null, (driver, now) => created = driver.SubmitDocument((DocumentType)request.DocumentType, request.MediaAssetId, request.IssuedAtUtc, request.ExpiresAtUtc, now), driver => DriverRepository.Document(created ?? driver.Documents.OrderByDescending(x => x.SubmittedAtUtc).First()), null, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> ReviewDocumentAsync(DriverActor actor, Guid driverId, Guid documentId, bool approve, DocumentReviewRequest request, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), approve ? "document.approve" : "document.reject", idempotencyKey, request, null, (driver, now) => { DriverDocument document = driver.Document(new DriverDocumentId(documentId)); if (document.ConcurrencyStamp != request.ConcurrencyStamp) throw new ConcurrencyException(); if (approve) document.Approve(actor.UserId, now); else document.Reject(actor.UserId, request.Reason ?? string.Empty, now); }, driver => DriverRepository.Map(driver)!, request.Reason, ct);

    public async Task<DriverOperationResult<DriverProfileResponse>> AssignZoneAsync(DriverActor actor, Guid driverId, AssignDriverZoneRequest request, string idempotencyKey, CancellationToken ct)
    {
        ServiceAreaDetails? area = await maps.GetServiceAreaAsync(request.ZoneId, ct); if (area is null || !area.IsActive) return Invalid<DriverProfileResponse>(DriverErrorCodes.ZoneInvalid);
        return await Execute(actor, new DriverId(driverId), "zone.assign", idempotencyKey, request, null, (driver, now) => driver.AssignZone(request.ZoneId, request.IsPrimary, actor.UserId, now), driver => DriverRepository.Map(driver)!, null, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> RemoveZoneAsync(DriverActor actor, Guid driverId, Guid zoneId, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), "zone.remove", idempotencyKey, new { zoneId }, null, (driver, now) => driver.RemoveZone(zoneId, now), driver => DriverRepository.Map(driver)!, null, ct);

    public Task<DriverOperationResult<DriverAvailabilityResponse>> ChangeAvailabilityAsync(DriverActor actor, string operation, string idempotencyKey, CancellationToken ct)
    {
        bool changed = true;
        return ExecuteByUser(actor, "availability." + operation, idempotencyKey, new { operation }, null, (driver, now) =>
        {
            switch (operation) { case "online": changed = driver.GoOnline(now); break; case "offline": changed = driver.GoOffline(now); break; case "break-start": driver.StartBreak(now); break; case "break-end": driver.EndBreak(now); break; default: throw new DomainException("Availability transition is invalid."); }
        }, DriverRepository.Availability, null, ct, () => changed);
    }

    public async Task<DriverOperationResult<DriverShiftResponse>> CreateShiftAsync(DriverActor actor, Guid driverId, CreateDriverShiftRequest request, string idempotencyKey, CancellationToken ct)
    {
        DriverShift? created = null; return await Execute(actor, new DriverId(driverId), "shift.create", idempotencyKey, request, null, (driver, now) => created = driver.ScheduleShift(request.ScheduledStartUtc, request.ScheduledEndUtc, now), driver => DriverRepository.Shift(created ?? driver.Shifts.OrderByDescending(x => x.CreatedAtUtc).First()), null, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> ChangeShiftAsync(DriverActor actor, Guid driverId, Guid shiftId, string operation, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), "shift." + operation, idempotencyKey, new { shiftId, operation }, null, (driver, now) => { DriverShift shift = driver.Shift(new DriverShiftId(shiftId)); switch (operation) { case "start": shift.Start(now); break; case "complete": shift.Complete(now); break; case "cancel": shift.Cancel(now); break; default: throw new DomainException("Shift transition is invalid."); } }, driver => DriverRepository.Map(driver)!, null, ct);

    public async Task<DriverOperationResult<IReadOnlyList<DriverShiftResponse>>> ListShiftsAsync(DriverActor actor, Guid driverId, CancellationToken ct)
    {
        Driver? driver = await SafeDriver(driverId, ct); return driver is null ? NotFound<IReadOnlyList<DriverShiftResponse>>() : Success<IReadOnlyList<DriverShiftResponse>>(MapShifts(driver));
    }

    public async Task<DriverOperationResult<DriverShiftResponse>> GetShiftAsync(DriverActor actor, Guid driverId, Guid shiftId, CancellationToken ct)
    {
        Driver? driver = await SafeDriver(driverId, ct); return ShiftResult(driver, shiftId);
    }

    public async Task<DriverOperationResult<IReadOnlyList<DriverShiftResponse>>> ListMyShiftsAsync(DriverActor actor, CancellationToken ct)
    {
        Driver? driver = await repository.GetByUserAsync(actor.UserId, ct); return driver is null ? NotFound<IReadOnlyList<DriverShiftResponse>>() : Success<IReadOnlyList<DriverShiftResponse>>(MapShifts(driver));
    }

    public async Task<DriverOperationResult<DriverShiftResponse>> GetMyShiftAsync(DriverActor actor, Guid shiftId, CancellationToken ct) => ShiftResult(await repository.GetByUserAsync(actor.UserId, ct), shiftId);

    public async Task<DriverOperationResult<DriverProfileResponse>> ChangeMyShiftAsync(DriverActor actor, Guid shiftId, string operation, string idempotencyKey, CancellationToken ct)
    {
        Driver? owned = await repository.GetByUserAsync(actor.UserId, ct); if (owned is null || owned.Shifts.All(x => x.Id.Value != shiftId)) return NotFound<DriverProfileResponse>();
        return await ExecuteByUser(actor, "shift.self." + operation, idempotencyKey, new { shiftId, operation }, null, (driver, now) =>
        {
            DriverShift shift = driver.Shift(new DriverShiftId(shiftId));
            switch (operation) { case "start": shift.Start(now); break; case "complete": shift.Complete(now); break; default: throw new DomainException("Self-service shift transition is invalid."); }
        }, driver => DriverRepository.Map(driver)!, null, ct);
    }

    public async Task<DriverOperationResult<DriverViolationResponse>> RecordViolationAsync(DriverActor actor, Guid driverId, RecordDriverViolationRequest request, string idempotencyKey, CancellationToken ct)
    {
        DriverViolation? created = null; return await Execute(actor, new DriverId(driverId), "violation.record", idempotencyKey, request, null, (driver, now) => created = driver.RecordViolation(request.ViolationType, (ViolationSeverity)request.Severity, request.Description, request.OccurredAtUtc, actor.UserId, now), driver => DriverRepository.Violation(created ?? driver.Violations.OrderByDescending(x => x.RecordedAtUtc).First()), null, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> ResolveViolationAsync(DriverActor actor, Guid driverId, Guid violationId, ResolveDriverViolationRequest request, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), "violation.resolve", idempotencyKey, request, null, (driver, now) => driver.Violation(new DriverViolationId(violationId)).Resolve(request.ResolutionNotes, now), driver => DriverRepository.Map(driver)!, null, ct);

    public async Task<DriverOperationResult<DriverSuspensionResponse>> SuspendAsync(DriverActor actor, Guid driverId, SuspendDriverRequest request, string idempotencyKey, CancellationToken ct)
    {
        DriverSuspension? created = null; return await Execute(actor, new DriverId(driverId), "suspension.create", idempotencyKey, request, null, (driver, now) => created = driver.Suspend(request.ReasonCode, request.Reason, request.StartsAtUtc, request.EndsAtUtc, actor.UserId, now), driver => DriverRepository.Suspension(created ?? driver.Suspensions.OrderByDescending(x => x.CreatedAtUtc).First()), request.ReasonCode, ct);
    }

    public Task<DriverOperationResult<DriverProfileResponse>> LiftSuspensionAsync(DriverActor actor, Guid driverId, Guid suspensionId, LiftDriverSuspensionRequest request, string idempotencyKey, CancellationToken ct) => Execute(actor, new DriverId(driverId), "suspension.lift", idempotencyKey, request, null, (driver, now) => driver.LiftSuspension(new DriverSuspensionId(suspensionId), actor.UserId, request.Reason, now), driver => DriverRepository.Map(driver)!, null, ct);

    public async Task<DriverEligibilitySnapshot?> GetAsync(Guid driverId, CancellationToken cancellationToken = default) => Snapshot(await repository.GetAsync(new DriverId(driverId), cancellationToken));
    public async Task<DriverEligibilitySnapshot?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default) => Snapshot(await repository.GetByUserAsync(userId, cancellationToken));

    private Task<DriverOperationResult<T>> ExecuteByUser<T>(DriverActor actor, string operation, string key, object request, Guid? stamp, Action<Driver, DateTime> action, Func<Driver, T> value, string? reason, CancellationToken ct, Func<bool>? hasBusinessChange = null) => ExecuteCore(actor, () => repository.GetByUserAsync(actor.UserId, ct), operation, key, request, stamp, action, value, reason, ct, hasBusinessChange);
    private Task<DriverOperationResult<T>> Execute<T>(DriverActor actor, DriverId id, string operation, string key, object request, Guid? stamp, Action<Driver, DateTime> action, Func<Driver, T> value, string? reason, CancellationToken ct) => ExecuteCore(actor, () => repository.GetAsync(id, ct), operation, key, request, stamp, action, value, reason, ct);

    private async Task<DriverOperationResult<T>> ExecuteCore<T>(DriverActor actor, Func<Task<Driver?>> load, string operation, string key, object request, Guid? stamp, Action<Driver, DateTime> action, Func<Driver, T> value, string? reason, CancellationToken ct, Func<bool>? hasBusinessChange = null)
    {
        string? keyHash = KeyHash(key); if (keyHash is null) return Invalid<T>(); string requestHash = RequestHash(request);
        DriverIdempotencyResult idempotency = await repository.CheckIdempotencyAsync(actor.UserId, operation, keyHash, requestHash, ct); if (idempotency.State == DriverIdempotencyState.DifferentRequest) return Conflict<T>(DriverErrorCodes.IdempotencyConflict); if (idempotency.State == DriverIdempotencyState.SameRequest) return Replay<T>(idempotency);
        Driver? driver = await load(); if (driver is null) return NotFound<T>();
        if (stamp.HasValue && driver.ConcurrencyStamp != stamp.Value) return Conflict<T>(DriverErrorCodes.ConcurrencyConflict);
        try
        {
            DateTime now = clock.UtcNow; action(driver, now); bool changed = hasBusinessChange?.Invoke() ?? true; T response = value(driver);
            DriverIdempotencyEntry idem = Idempotency(actor.UserId, operation, keyHash, requestHash, driver.Id, DriverOperationStatus.Success, response, now);
            DriverAuditEntry? audit = changed ? Audit(actor, driver, operation, now, reason) : null;
            IReadOnlyCollection<IIntegrationEvent> events = changed ? Events(driver, operation, now) : [];
            if (!await repository.SaveOperationAsync(driver, idem, audit, events, ct))
            {
                DriverIdempotencyResult duplicate = await repository.CheckIdempotencyAsync(actor.UserId, operation, keyHash, requestHash, ct);
                return duplicate.State == DriverIdempotencyState.SameRequest ? Replay<T>(duplicate) : Conflict<T>(DriverErrorCodes.ConcurrencyConflict);
            }
            return Success(response);
        }
        catch (ConcurrencyException) { return Conflict<T>(DriverErrorCodes.ConcurrencyConflict); }
        catch (DomainException) { return Invalid<T>(); }
    }

    private async Task<bool> IsReadyMedia(Guid id, CancellationToken ct) { MediaAssetReference? asset = await media.FindAsync(id, ct); return asset is { IsReady: true, IsDeleted: false }; }
    private async Task<Driver?> SafeDriver(Guid id, CancellationToken ct) { try { return await repository.GetAsync(new DriverId(id), ct); } catch (DomainException) { return null; } }
    private async Task<DriverProfileResponse?> SafeProfile(Guid id, CancellationToken ct) { try { return await repository.GetProfileAsync(new DriverId(id), ct); } catch (DomainException) { return null; } }
    private static DriverAuditEntry Audit(DriverActor actor, Driver driver, string action, DateTime now, string? reason) => new(actor.UserId, driver.Id, action, now, actor.CorrelationId, reason);
    private static List<IIntegrationEvent> Events(Driver driver, string operation, DateTime occurredAtUtc)
    {
        List<IIntegrationEvent> events = [];
        foreach (IDomainEvent domainEvent in driver.DomainEvents) switch (domainEvent) { case DriverActivationChangedDomainEvent e when e.Status == DriverActivationStatus.Approved: events.Add(new DriverActivatedIntegrationEvent(Guid.NewGuid(), 1, e.DriverId, e.OccurredAtUtc)); break; case DriverAvailabilityChangedDomainEvent e: events.Add(new DriverAvailabilityChangedIntegrationEvent(Guid.NewGuid(), 1, e.DriverId, (short)e.Current, e.OccurredAtUtc)); break; case DriverSuspendedDomainEvent e: events.Add(new DriverSuspendedIntegrationEvent(Guid.NewGuid(), 1, e.DriverId, e.OccurredAtUtc)); break; case DriverVehicleChangedDomainEvent e: events.Add(new DriverVehicleChangedIntegrationEvent(Guid.NewGuid(), 1, e.DriverId, e.VehicleId, e.OccurredAtUtc)); break; }
        if (events.Count == 0) events.Add(new DriverOperationCompletedIntegrationEvent(Guid.NewGuid(), 1, driver.Id.Value, operation, occurredAtUtc));
        return events;
    }
    private DriverEligibilitySnapshot? Snapshot(Driver? driver)
    {
        if (driver is null) return null; DateTime now = clock.UtcNow;
        return new(driver.Id.Value, driver.IsOperationallyActiveAt(now), driver.ActivationStatus == DriverActivationStatus.Approved, (short)driver.AvailabilityStatus, driver.Vehicles.FirstOrDefault(x => x.IsPrimary && x.Status == VehicleStatus.Active) is { } vehicle ? (short)vehicle.Type : null, driver.ZoneAssignments.Where(x => x.IsActive).Select(x => x.ZoneId).ToList(), driver.MaximumConcurrentDeliveries, driver.CurrentLoad, driver.HasActiveSuspension(now));
    }
    private static string? KeyHash(string value) => string.IsNullOrWhiteSpace(value) || value.Length > 200 ? null : Hash(value.Trim());
    private static string RequestHash(object value)
    {
        JsonElement element = JsonSerializer.SerializeToElement(value, JsonOptions);
        using MemoryStream stream = new(); using (Utf8JsonWriter writer = new(stream)) WriteCanonical(writer, element);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object: writer.WriteStartObject(); foreach (JsonProperty property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); } writer.WriteEndObject(); break;
            case JsonValueKind.Array: writer.WriteStartArray(); foreach (JsonElement item in element.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); break;
            default: element.WriteTo(writer); break;
        }
    }
    private static DriverIdempotencyEntry Idempotency<T>(Guid actorUserId, string operation, string keyHash, string requestHash, DriverId driverId, DriverOperationStatus status, T response, DateTime now) => new(actorUserId, operation, keyHash, requestHash, driverId, status, JsonSerializer.Serialize(response, JsonOptions), now);
    private static DriverOperationResult<T> Replay<T>(DriverIdempotencyResult result)
    {
        if (result.ResponseStatus is null || string.IsNullOrWhiteSpace(result.ResponseJson)) return Conflict<T>(DriverErrorCodes.IdempotencyConflict);
        T? response = JsonSerializer.Deserialize<T>(result.ResponseJson, JsonOptions);
        return response is null ? Conflict<T>(DriverErrorCodes.IdempotencyConflict) : new DriverOperationResult<T>(result.ResponseStatus.Value, response);
    }
    private static List<DriverShiftResponse> MapShifts(Driver driver) => driver.Shifts.OrderBy(x => x.ScheduledStartUtc).Select(DriverRepository.Shift).ToList();
    private static DriverOperationResult<DriverShiftResponse> ShiftResult(Driver? driver, Guid shiftId)
    {
        if (driver is null || shiftId == Guid.Empty) return NotFound<DriverShiftResponse>();
        DriverShift? shift = driver.Shifts.SingleOrDefault(x => x.Id.Value == shiftId); return shift is null ? NotFound<DriverShiftResponse>() : Success(DriverRepository.Shift(shift));
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static DriverOperationResult<T> From<T>(T? value) => value is null ? NotFound<T>() : Success(value);
    private static DriverOperationResult<T> Success<T>(T value) => new(DriverOperationStatus.Success, value);
    private static DriverOperationResult<T> NotFound<T>() => new(DriverOperationStatus.NotFound, default, DriverErrorCodes.NotFound);
    private static DriverOperationResult<T> Invalid<T>(string code = DriverErrorCodes.InvalidRequest) => new(DriverOperationStatus.Invalid, default, code);
    private static DriverOperationResult<T> Conflict<T>(string code = DriverErrorCodes.Conflict) => new(DriverOperationStatus.Conflict, default, code);
    private sealed class ConcurrencyException : Exception;
}
