using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Dispatching.Contracts;
using AlSsareea.Modules.Dispatching.Domain;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Maps.Contracts;
using AlSsareea.Modules.Tracking.Contracts;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Dispatching.Application;

public sealed class DispatchingOptions
{
    public const string SectionName = "Dispatching";
    public int OfferLifetimeSeconds { get; init; } = 30;
    public int MaximumAttempts { get; init; } = 3;
    public int MaximumCandidateCount { get; init; } = 50;
    public int MaximumLocationStalenessSeconds { get; init; } = 300;
    public double MaximumLocationAccuracyMeters { get; init; } = 250;
}
public static class DispatchErrorCodes
{
    public const string NotFound = "dispatching.not_found"; public const string Invalid = "dispatching.invalid_request"; public const string Conflict = "dispatching.conflict"; public const string Forbidden = "dispatching.forbidden"; public const string IdempotencyConflict = "dispatching.idempotency_conflict"; public const string DeliveryInvalid = "dispatching.delivery_invalid"; public const string NoCandidates = "dispatching.no_eligible_candidates"; public const string AssignmentFailed = "dispatching.assignment_failed";
}
public enum DispatchOperationStatus { Success, Created, NotFound, Invalid, Conflict, Forbidden, Unprocessable }
public sealed record DispatchOperationResult<T>(DispatchOperationStatus Status, T? Value = default, string? ErrorCode = null);
public sealed record DispatchActor(Guid UserId, string? CorrelationId);
public sealed record DispatchIdempotencyResult(Guid DispatchRequestId, string RequestHash);
public sealed record DispatchAuditEntry(Guid ActorUserId, DispatchRequestId DispatchRequestId, string Operation, DispatchStatus OldStatus, DispatchStatus NewStatus, DateTime OccurredAtUtc, string? CorrelationId, string IdempotencyKeyHash, string? Reason);
public static class DispatchCandidateEligibility
{
    public static bool IsEligible(DriverDispatchCandidateSnapshot? driver, Guid zoneId, short? vehicleType, bool requireZone = true) => driver is { IsActive: true, IsApproved: true, HasActiveSuspension: false } && driver.AvailabilityStatus is 2 or 3 && driver.CurrentLoad < driver.MaximumCapacity && (!requireZone || driver.ActiveZoneIds.Contains(zoneId)) && (!vehicleType.HasValue || driver.PrimaryVehicleType == vehicleType);
    public static bool IsEligible(DriverEligibilitySnapshot? driver, Guid zoneId, short? vehicleType, bool requireZone = true) => driver is { IsActive: true, IsApproved: true, HasActiveSuspension: false } && driver.AvailabilityStatus is 2 or 3 && driver.CurrentLoad < driver.MaximumCapacity && (!requireZone || driver.ActiveZoneIds.Contains(zoneId)) && (!vehicleType.HasValue || driver.PrimaryVehicleType == vehicleType);
}

public interface IDispatchRepository
{
    Task<DispatchRequest?> GetAsync(DispatchRequestId id, bool noTracking, CancellationToken cancellationToken);
    Task<DispatchRequest?> GetByDeliveryAsync(Guid deliveryId, bool noTracking, CancellationToken cancellationToken);
    Task<DispatchIdempotencyResult?> FindIdempotencyAsync(Guid actorId, string operation, string keyHash, CancellationToken cancellationToken);
    Task<bool> CreateAsync(DispatchRequest request, Guid actorId, string keyHash, string requestHash, IReadOnlyCollection<IIntegrationEvent> events, CancellationToken cancellationToken);
    Task<bool> SaveAsync(DispatchRequest request, Guid actorId, string operation, string keyHash, string requestHash, DispatchAuditEntry audit, IReadOnlyCollection<IIntegrationEvent> events, CancellationToken cancellationToken);
    Task<IReadOnlyList<DispatchRequest>> GetExpiredAsync(DateTime now, int count, CancellationToken cancellationToken);
}
public interface IDispatchService
{
    Task<DispatchOperationResult<DispatchResponse>> StartAsync(DispatchActor actor, StartDispatchRequest request, string idempotencyKey, CancellationToken ct);
    Task<DispatchOperationResult<DispatchResponse>> GetAsync(Guid id, CancellationToken ct);
    Task<DispatchOperationResult<DispatchResponse>> AcceptAsync(DispatchActor actor, Guid id, Guid offerId, string idempotencyKey, CancellationToken ct);
    Task<DispatchOperationResult<DispatchResponse>> DeclineAsync(DispatchActor actor, Guid id, Guid offerId, OfferDecisionRequest request, string key, CancellationToken ct);
    Task<DispatchOperationResult<DispatchResponse>> RetryAsync(DispatchActor actor, Guid id, RetryDispatchRequest request, string key, CancellationToken ct);
    Task<DispatchOperationResult<DispatchResponse>> CancelAsync(DispatchActor actor, Guid id, CancelDispatchRequest request, string key, CancellationToken ct);
    Task<DispatchOperationResult<DispatchResponse>> ManualAssignAsync(DispatchActor actor, Guid id, ManualAssignDispatchRequest request, string key, CancellationToken ct);
    Task ExpireAsync(CancellationToken ct);
}

public sealed class DispatchService(IDispatchRepository repository, IDriverDispatchCandidateProvider drivers, IDriverEligibilityProvider driverEligibility, IDriverOperationalSnapshotProvider operationalDrivers, IDispatchLocationProvider locations, IRoutingProvider routing, IDispatchDeliveryProvider deliveries, IClock clock, IOptions<DispatchingOptions> options) : IDispatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DispatchingOptions settings = options.Value;

    public async Task<DispatchOperationResult<DispatchResponse>> StartAsync(DispatchActor actor, StartDispatchRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (!TryRequest(actor, idempotencyKey, request, out string keyHash, out string requestHash) || request.DeliveryId == Guid.Empty || request.ZoneId == Guid.Empty) return Invalid();
        DispatchOperationResult<DispatchResponse>? replay = await Replay(actor.UserId, "start", keyHash, requestHash, ct); if (replay is not null) return replay;
        DispatchDeliverySnapshot? delivery = await deliveries.GetAsync(request.DeliveryId, ct); if (delivery is null || delivery.DriverId.HasValue || delivery.Status != 1 || !delivery.PickupLatitude.HasValue || !delivery.PickupLongitude.HasValue) return Failure(DispatchOperationStatus.Invalid, DispatchErrorCodes.DeliveryInvalid);
        if (await repository.GetByDeliveryAsync(request.DeliveryId, true, ct) is not null) return Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.Conflict);
        DateTime now = clock.UtcNow; DispatchRequest dispatch = DispatchRequest.Create(DispatchRequestId.New(), delivery.DeliveryId, delivery.OrderId, delivery.MerchantId, delivery.BranchId, request.ZoneId, delivery.PickupLatitude.Value, delivery.PickupLongitude.Value, request.RequiredVehicleType, request.PreparationSeconds, now);
        IReadOnlyList<DispatchCandidate> candidates = await Evaluate(dispatch, ct); dispatch.StartAttempt(candidates, settings.MaximumAttempts, now);
        List<IIntegrationEvent> events = [new DispatchRequestedIntegrationEvent(Guid.NewGuid(), 1, dispatch.Id.Value, dispatch.DeliveryId, now)]; AddOfferEvent(dispatch, events, now); if (dispatch.Status == DispatchStatus.Failed) events.Add(new DispatchFailedIntegrationEvent(Guid.NewGuid(), 1, dispatch.Id.Value, dispatch.DeliveryId, dispatch.FailureReason!, now));
        bool saved = await repository.CreateAsync(dispatch, actor.UserId, keyHash, requestHash, events, ct); return saved ? new(DispatchOperationStatus.Created, Map(dispatch)) : Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.Conflict);
    }
    public async Task<DispatchOperationResult<DispatchResponse>> GetAsync(Guid id, CancellationToken ct) { DispatchRequest? value = await SafeGet(id, true, ct); return value is null ? Failure(DispatchOperationStatus.NotFound, DispatchErrorCodes.NotFound) : Success(value); }

    public async Task<DispatchOperationResult<DispatchResponse>> AcceptAsync(DispatchActor actor, Guid id, Guid offerId, string idempotencyKey, CancellationToken ct)
    {
        object payload = new { id, offerId }; if (!TryRequest(actor, idempotencyKey, payload, out string keyHash, out string requestHash)) return Invalid();
        DispatchOperationResult<DispatchResponse>? replay = await Replay(actor.UserId, "accept", keyHash, requestHash, ct); if (replay is not null) { if (replay.Value?.AssignedDriverId is Guid assigned) _ = await deliveries.AssignAsync(replay.Value.DeliveryId, assigned, replay.Value.Id, ct); return replay; }
        DriverEligibilitySnapshot? driver = await operationalDrivers.GetByUserAsync(actor.UserId, ct); if (driver is null) return Failure(DispatchOperationStatus.NotFound, DispatchErrorCodes.NotFound);
        DispatchRequest? dispatch = await SafeGet(id, false, ct); if (dispatch is null) return Failure(DispatchOperationStatus.NotFound, DispatchErrorCodes.NotFound);
        DispatchStatus old = dispatch.Status; DateTime now = clock.UtcNow;
        try { dispatch.Accept(offerId, driver.DriverId, now); } catch (DomainException) { return Failure(DispatchOperationStatus.Unprocessable, DispatchErrorCodes.Conflict); }
        IIntegrationEvent[] events = [new DispatchOfferAcceptedIntegrationEvent(Guid.NewGuid(), 1, dispatch.Id.Value, offerId, driver.DriverId, now), new DriverAssignedIntegrationEvent(Guid.NewGuid(), 1, dispatch.Id.Value, dispatch.DeliveryId, driver.DriverId, now)];
        bool won = await repository.SaveAsync(dispatch, actor.UserId, "accept", keyHash, requestHash, Audit(actor, dispatch, "accept", old, now, null, keyHash), events, ct); if (!won) return Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.Conflict);
        DispatchAssignmentResult assignment = await deliveries.AssignAsync(dispatch.DeliveryId, driver.DriverId, dispatch.Id.Value, ct); return assignment.Status is DispatchAssignmentStatus.Applied or DispatchAssignmentStatus.AlreadyApplied ? Success(dispatch) : Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.AssignmentFailed);
    }
    public async Task<DispatchOperationResult<DispatchResponse>> DeclineAsync(DispatchActor actor, Guid id, Guid offerId, OfferDecisionRequest request, string key, CancellationToken ct)
    {
        Guid driverId; try { driverId = await ResolveOwnedDriver(actor, ct); } catch (DomainException) { return Failure(DispatchOperationStatus.NotFound, DispatchErrorCodes.NotFound); }
        return await Mutate(actor, id, "decline", new { offerId, request.Reason }, key, (dispatch, now) => dispatch.Decline(offerId, driverId, request.Reason, now, OfferLifetime), request.Reason, ct);
    }
    public async Task<DispatchOperationResult<DispatchResponse>> RetryAsync(DispatchActor actor, Guid id, RetryDispatchRequest request, string key, CancellationToken ct)
    {
        return await MutateAsync(actor, id, "retry", request, key, async (dispatch, now) => { if (dispatch.Status != DispatchStatus.Failed) throw new DomainException("Only failed dispatch can retry."); IReadOnlyList<DispatchCandidate> candidates = await Evaluate(dispatch, ct); dispatch.StartAttempt(candidates, settings.MaximumAttempts, now); }, request.Reason, ct);
    }
    public Task<DispatchOperationResult<DispatchResponse>> CancelAsync(DispatchActor actor, Guid id, CancelDispatchRequest request, string key, CancellationToken ct) => Mutate(actor, id, "cancel", request, key, (dispatch, now) => dispatch.Cancel(request.Reason, now), request.Reason, ct);
    public async Task<DispatchOperationResult<DispatchResponse>> ManualAssignAsync(DispatchActor actor, Guid id, ManualAssignDispatchRequest request, string key, CancellationToken ct)
    {
        DispatchRequest? target = await SafeGet(id, true, ct); if (target is null) return Failure(DispatchOperationStatus.NotFound, DispatchErrorCodes.NotFound); DriverEligibilitySnapshot? driver = await driverEligibility.GetAsync(request.DriverId, ct); if (!DispatchCandidateEligibility.IsEligible(driver, target.ZoneId, target.RequiredVehicleType)) return Failure(DispatchOperationStatus.Invalid, DispatchErrorCodes.NoCandidates);
        DispatchOperationResult<DispatchResponse> result = await Mutate(actor, id, "manual-assign", request, key, (dispatch, now) => dispatch.ManualAssign(request.DriverId, actor.UserId, request.Reason, now), request.Reason, ct);
        if (result.Value is { } value) { DispatchAssignmentResult assignment = await deliveries.AssignAsync(value.DeliveryId, request.DriverId, value.Id, ct); if (assignment.Status is not (DispatchAssignmentStatus.Applied or DispatchAssignmentStatus.AlreadyApplied)) return Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.AssignmentFailed); }
        return result;
    }
    public async Task ExpireAsync(CancellationToken ct) { DateTime now = clock.UtcNow; foreach (DispatchRequest request in await repository.GetExpiredAsync(now, 100, ct)) { DispatchStatus old = request.Status; if (!request.ExpireActive(now, OfferLifetime)) continue; string hash = Hash("expiration:" + request.Id.Value + ":" + now.Ticks); await repository.SaveAsync(request, request.Id.Value, "expire", hash, hash, new(request.Id.Value, request.Id, "expire", old, request.Status, now, null, hash, null), [], ct); } }

    private async Task<DispatchOperationResult<DispatchResponse>> Mutate(DispatchActor actor, Guid id, string operation, object payload, string key, Action<DispatchRequest, DateTime> action, string? reason, CancellationToken ct) => await MutateAsync(actor, id, operation, payload, key, (dispatch, now) => { action(dispatch, now); return Task.CompletedTask; }, reason, ct);
    private async Task<DispatchOperationResult<DispatchResponse>> MutateAsync(DispatchActor actor, Guid id, string operation, object payload, string key, Func<DispatchRequest, DateTime, Task> action, string? reason, CancellationToken ct)
    {
        if (!TryRequest(actor, key, payload, out string keyHash, out string requestHash)) return Invalid(); DispatchOperationResult<DispatchResponse>? replay = await Replay(actor.UserId, operation, keyHash, requestHash, ct); if (replay is not null) return replay;
        DispatchRequest? dispatch = await SafeGet(id, false, ct); if (dispatch is null) return Failure(DispatchOperationStatus.NotFound, DispatchErrorCodes.NotFound); DispatchStatus old = dispatch.Status; DateTime now = clock.UtcNow;
        try { await action(dispatch, now); } catch (DomainException) { return Failure(DispatchOperationStatus.Unprocessable, DispatchErrorCodes.Conflict); }
        List<IIntegrationEvent> events = []; AddOfferEvent(dispatch, events, now); if (dispatch.Status == DispatchStatus.Failed) events.Add(new DispatchFailedIntegrationEvent(Guid.NewGuid(), 1, dispatch.Id.Value, dispatch.DeliveryId, dispatch.FailureReason!, now));
        return await repository.SaveAsync(dispatch, actor.UserId, operation, keyHash, requestHash, Audit(actor, dispatch, operation, old, now, reason, keyHash), events, ct) ? Success(dispatch) : Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.Conflict);
    }
    private async Task<IReadOnlyList<DispatchCandidate>> Evaluate(DispatchRequest request, CancellationToken ct)
    {
        DateTime now = clock.UtcNow; IReadOnlyList<DriverDispatchCandidateSnapshot> discovered = await drivers.FindAsync(request.ZoneId, request.RequiredVehicleType, Math.Clamp(settings.MaximumCandidateCount, 1, DispatchRules.MaximumCandidates), ct); var evaluated = new List<(DriverDispatchCandidateSnapshot Driver, long Distance, int Eta, CandidateScore Score)>();
        foreach (DriverDispatchCandidateSnapshot driver in discovered)
        {
            if (!DispatchCandidateEligibility.IsEligible(driver, request.ZoneId, request.RequiredVehicleType)) continue; DispatchDriverLocation? location = await locations.GetLatestAsync(driver.DriverId, ct); if (location is null || location.RecordedAtUtc < now.AddSeconds(-settings.MaximumLocationStalenessSeconds) || location.AccuracyMeters > settings.MaximumLocationAccuracyMeters) continue;
            RouteResult route = await routing.CalculateRouteAsync(new(new(location.Latitude, location.Longitude), new(request.PickupLatitude, request.PickupLongitude)), ct); int eta = checked((int)Math.Ceiling(route.EstimatedDuration.TotalSeconds)); CandidateScore score = DispatchScoringPolicy.Score(new(route.DistanceMeters, eta, driver.CurrentLoad, driver.MaximumCapacity, driver.LastAssignmentAtUtc, request.PreparationSeconds, driver.DriverId), now); evaluated.Add((driver, route.DistanceMeters, eta, score));
        }
        var result = new List<DispatchCandidate>(); int rank = 1; foreach (var item in evaluated.OrderByDescending(x => x.Score.Score).ThenBy(x => x.Distance).ThenBy(x => x.Driver.DriverId)) result.Add(DispatchCandidate.Create(request.Id, item.Driver.DriverId, request.AttemptNumber + 1, item.Distance, item.Eta, item.Driver.CurrentLoad, item.Driver.MaximumCapacity, item.Driver.LastAssignmentAtUtc, item.Score.Score, rank++, item.Score.Explanation, now)); return result;
    }
    private async Task<Guid> ResolveOwnedDriver(DispatchActor actor, CancellationToken ct) => (await operationalDrivers.GetByUserAsync(actor.UserId, ct))?.DriverId ?? throw new DomainException("Driver was not found.");
    private async Task<DispatchRequest?> SafeGet(Guid id, bool noTracking, CancellationToken ct) { if (id == Guid.Empty) return null; return await repository.GetAsync(new(id), noTracking, ct); }
    private async Task<DispatchOperationResult<DispatchResponse>?> Replay(Guid actor, string operation, string keyHash, string requestHash, CancellationToken ct) { DispatchIdempotencyResult? duplicate = await repository.FindIdempotencyAsync(actor, operation, keyHash, ct); if (duplicate is null) return null; if (duplicate.RequestHash != requestHash) return Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.IdempotencyConflict); DispatchRequest? value = await repository.GetAsync(new(duplicate.DispatchRequestId), true, ct); return value is null ? Failure(DispatchOperationStatus.Conflict, DispatchErrorCodes.IdempotencyConflict) : Success(value); }
    private static void AddOfferEvent(DispatchRequest dispatch, List<IIntegrationEvent> events, DateTime now) { DispatchOffer? offer = dispatch.Offers.Where(x => x.Status == DispatchOfferStatus.Pending).OrderByDescending(x => x.Sequence).FirstOrDefault(); if (offer is not null) events.Add(new DispatchOfferCreatedIntegrationEvent(Guid.NewGuid(), 1, dispatch.Id.Value, offer.Id.Value, offer.DriverId, offer.ExpiresAtUtc, now)); }
    private static DispatchAuditEntry Audit(DispatchActor actor, DispatchRequest request, string operation, DispatchStatus old, DateTime now, string? reason, string hash) => new(actor.UserId, request.Id, operation, old, request.Status, now, actor.CorrelationId, hash, reason);
    private TimeSpan OfferLifetime => TimeSpan.FromSeconds(Math.Clamp(settings.OfferLifetimeSeconds, 5, 600));
    private static bool TryRequest(DispatchActor actor, string key, object payload, out string keyHash, out string requestHash) { keyHash = requestHash = string.Empty; if (actor.UserId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > DispatchRules.IdempotencyKeyMaximumLength) return false; keyHash = Hash(key.Trim()); requestHash = Hash(JsonSerializer.Serialize(payload, payload.GetType(), JsonOptions)); return true; }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static DispatchOperationResult<DispatchResponse> Success(DispatchRequest value) => new(DispatchOperationStatus.Success, Map(value)); private static DispatchOperationResult<DispatchResponse> Invalid() => Failure(DispatchOperationStatus.Invalid, DispatchErrorCodes.Invalid); private static DispatchOperationResult<DispatchResponse> Failure(DispatchOperationStatus status, string code) => new(status, null, code);
    public static DispatchResponse Map(DispatchRequest x) => new(x.Id.Value, x.DeliveryId, x.OrderId, x.MerchantId, x.MerchantBranchId, x.ZoneId, (short)x.Status, x.AttemptNumber, x.AssignedDriverId, x.CreatedAtUtc, x.UpdatedAtUtc, x.CompletedAtUtc, x.FailureReason, x.ConcurrencyStamp, x.Candidates.OrderBy(c => c.AttemptNumber).ThenBy(c => c.Rank).Select(c => new DispatchCandidateResponse(c.Id.Value, c.DriverId, c.DistanceMeters, c.EtaSeconds, c.CurrentLoad, c.MaximumCapacity, c.Score, c.Rank, c.Explanation, c.CreatedAtUtc)).ToArray(), x.Offers.OrderBy(o => o.Sequence).Select(o => new DispatchOfferResponse(o.Id.Value, o.DriverId, o.Sequence, (short)o.Status, o.OfferedAtUtc, o.ExpiresAtUtc, o.RespondedAtUtc, o.DeclineReason)).ToArray(), x.History.OrderBy(h => h.OccurredAtUtc).ThenBy(h => h.Id.Value).Select(h => new DispatchHistoryResponse(h.Id.Value, h.AttemptNumber, (short)h.Type, h.OfferId, h.DriverId, h.Detail, h.OccurredAtUtc)).ToArray());
}
