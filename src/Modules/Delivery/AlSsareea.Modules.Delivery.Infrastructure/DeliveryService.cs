using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.BuildingBlocks.Contracts;
using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Delivery.Application;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Delivery.Domain;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Media.Contracts;
using AlSsareea.Modules.Orders.Contracts;
using DeliveryAggregate = AlSsareea.Modules.Delivery.Domain.Delivery;

namespace AlSsareea.Modules.Delivery.Infrastructure;

internal sealed class DeliveryService(
    IDeliveryRepository repository,
    IDeliveryOrderSnapshotProvider orders,
    ICustomerIdentityProvider customers,
    IDriverEligibilityProvider drivers,
    IDriverOperationalSnapshotProvider operationalDrivers,
    IMediaAssetLookup media,
    IDeliveryPinProtector pins,
    IClock clock) : IDeliveryService, IDispatchDeliveryProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DispatchDeliverySnapshot?> GetAsync(Guid deliveryId, CancellationToken ct = default)
    {
        DeliveryAggregate? delivery = await SafeGet(deliveryId, true, ct);
        return delivery is null ? null : new(delivery.Id.Value, delivery.OrderId, delivery.MerchantId, delivery.BranchId, (short)delivery.Status, delivery.DriverId, delivery.Pickup.Latitude, delivery.Pickup.Longitude);
    }

    public async Task<DispatchAssignmentResult> AssignAsync(Guid deliveryId, Guid driverId, Guid assignmentId, CancellationToken ct = default)
    {
        if (deliveryId == Guid.Empty || driverId == Guid.Empty || assignmentId == Guid.Empty) return new(DispatchAssignmentStatus.Invalid, null, DeliveryErrorCodes.InvalidRequest);
        DeliveryAggregate? delivery = await SafeGet(deliveryId, false, ct); if (delivery is null) return new(DispatchAssignmentStatus.NotFound, null, DeliveryErrorCodes.NotFound);
        if (delivery.DriverId.HasValue) return delivery.DriverId == driverId ? new(DispatchAssignmentStatus.AlreadyApplied, driverId) : new(DispatchAssignmentStatus.Conflict, delivery.DriverId, DeliveryErrorCodes.ConcurrencyConflict);
        DriverEligibilitySnapshot? driver = await drivers.GetAsync(driverId, ct); if (driver is null || !driver.IsActive || !driver.IsApproved || driver.HasActiveSuspension || driver.AvailabilityStatus is not (2 or 3) || driver.CurrentLoad >= driver.MaximumCapacity) return new(DispatchAssignmentStatus.Invalid, null, DeliveryErrorCodes.DriverIneligible);
        DeliveryStatus old = delivery.Status; DateTime now = clock.UtcNow;
        try { delivery.Assign(driverId, now); } catch (DomainException) { return new(DispatchAssignmentStatus.Invalid, null, DeliveryErrorCodes.InvalidTransition); }
        string hash = Hash(assignmentId.ToString("N")); DeliveryAuditEntry audit = new(assignmentId, delivery.Id, "dispatch.assign", old, delivery.Status, now, null, hash, null);
        IIntegrationEvent[] events = [new DeliveryDriverAssignedIntegrationEvent(Guid.NewGuid(), 1, delivery.Id.Value, delivery.OrderId, driverId, now)];
        return await repository.SaveOperationAsync(delivery, assignmentId, "dispatch.assign", hash, Hash(driverId.ToString("N")), audit, events, ct) ? new(DispatchAssignmentStatus.Applied, driverId) : new(DispatchAssignmentStatus.Conflict, null, DeliveryErrorCodes.ConcurrencyConflict);
    }

    public async Task<DeliveryOperationResult<DeliveryCreatedResponse>> CreateAsync(DeliveryActor actor, CreateDeliveryRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (!TryRequest(actor, idempotencyKey, request, out string keyHash, out string requestHash)) return Invalid<DeliveryCreatedResponse>(DeliveryErrorCodes.InvalidRequest);
        DeliveryOperationResult<DeliveryCreatedResponse>? duplicate = await DuplicateCreated(actor.UserId, keyHash, requestHash, ct);
        if (duplicate is not null) return duplicate;
        if ((request.ProofRequirements & ~15) != 0) return Invalid<DeliveryCreatedResponse>(DeliveryErrorCodes.InvalidRequest);

        DeliveryOrderSnapshot? order = await orders.GetAsync(request.OrderId, ct);
        if (order is null) return Invalid<DeliveryCreatedResponse>(DeliveryErrorCodes.OrderInvalid);
        if (!order.IsEligible) return Invalid<DeliveryCreatedResponse>(DeliveryErrorCodes.OrderIneligible);
        Guid? customerUserId = await customers.GetUserIdAsync(order.CustomerId, ct);
        if (!customerUserId.HasValue) return Invalid<DeliveryCreatedResponse>(DeliveryErrorCodes.OrderInvalid);
        if (await repository.GetByOrderAsync(order.OrderId, true, ct) is not null) return Conflict<DeliveryCreatedResponse>(DeliveryErrorCodes.OrderAlreadyHasDelivery);

        try
        {
            ProofRequirement requirements = (ProofRequirement)request.ProofRequirements;
            DeliveryPinSecret? secret = (requirements & ProofRequirement.Pin) != 0 ? pins.Generate() : null;
            PickupSnapshot pickup = new(order.MerchantId, order.BranchId, order.PickupAddress, order.PickupContactName, order.PickupPhoneNumber, order.PickupInstructions, order.PickupLatitude, order.PickupLongitude);
            DropOffSnapshot dropOff = new(order.DropOffAddressId, order.DropOffAddress, order.RecipientName, order.RecipientPhoneNumber, order.DropOffFloor, order.DropOffInstructions, order.DropOffLatitude, order.DropOffLongitude);
            DateTime now = clock.UtcNow;
            DeliveryAggregate delivery = DeliveryAggregate.Create(DeliveryId.New(), order.OrderId, order.CustomerId, customerUserId.Value, pickup, dropOff, requirements, secret?.Hash, secret?.Salt, now);
            IIntegrationEvent[] events = [new DeliveryCreatedIntegrationEvent(Guid.NewGuid(), 1, delivery.Id.Value, delivery.OrderId, delivery.CustomerId, delivery.MerchantId, now)];
            if (!await repository.CreateAsync(delivery, actor.UserId, keyHash, requestHash, events, ct)) return Conflict<DeliveryCreatedResponse>(DeliveryErrorCodes.OrderAlreadyHasDelivery);
            return new(DeliveryOperationStatus.Created, new(Map(delivery), secret?.Pin));
        }
        catch (DomainException) { return Invalid<DeliveryCreatedResponse>(DeliveryErrorCodes.InvalidRequest); }
    }

    public async Task<DeliveryOperationResult<DeliveryResponse>> AssignAsync(DeliveryActor actor, Guid deliveryId, AssignDeliveryRequest request, string idempotencyKey, CancellationToken ct)
    {
        DriverEligibilitySnapshot? driver = await drivers.GetAsync(request.DriverId, ct);
        if (driver is null) return Invalid<DeliveryResponse>(DeliveryErrorCodes.DriverInvalid);
        if (!driver.IsActive || !driver.IsApproved || driver.HasActiveSuspension || driver.CurrentLoad >= driver.MaximumCapacity) return Invalid<DeliveryResponse>(DeliveryErrorCodes.DriverIneligible);
        return await Mutate(actor, deliveryId, "assign", request, idempotencyKey, request.ConcurrencyStamp, false,
            (delivery, now) => delivery.Assign(request.DriverId, now), null, ct);
    }

    public async Task<DeliveryOperationResult<DeliveryResponse>> GetForCustomerAsync(DeliveryActor actor, Guid deliveryId, CancellationToken ct)
    {
        DeliveryAggregate? delivery = await SafeGet(deliveryId, true, ct);
        return delivery is null || delivery.CustomerUserId != actor.UserId ? NotFound<DeliveryResponse>() : Success(Map(delivery));
    }

    public async Task<DeliveryOperationResult<DeliveryResponse>> GetCurrentForCustomerAsync(DeliveryActor actor, CancellationToken ct)
    {
        DeliveryAggregate? delivery = await repository.GetCurrentForCustomerAsync(actor.UserId, ct);
        return delivery is null ? NotFound<DeliveryResponse>() : Success(Map(delivery));
    }

    public async Task<DeliveryOperationResult<DeliveryResponse>> GetCurrentForDriverAsync(DeliveryActor actor, CancellationToken ct)
    {
        DriverEligibilitySnapshot? driver = await operationalDrivers.GetByUserAsync(actor.UserId, ct);
        if (driver is null) return NotFound<DeliveryResponse>();
        DeliveryAggregate? delivery = await repository.GetCurrentForDriverAsync(driver.DriverId, ct);
        return delivery is null ? NotFound<DeliveryResponse>() : Success(Map(delivery));
    }

    public Task<DeliveryOperationResult<DeliveryResponse>> TransitionAsync(DeliveryActor actor, Guid deliveryId, string operation, DeliveryTransitionRequest request, string idempotencyKey, CancellationToken ct)
    {
        Action<DeliveryAggregate, DateTime>? action = operation switch
        {
            "heading-to-pickup" => (delivery, now) => delivery.BeginHeadingToPickup(now),
            "arrive-at-pickup" => (delivery, now) => delivery.ArriveAtPickup(now),
            "confirm-pickup" => (delivery, now) => delivery.ConfirmPickup(now),
            "start" => (delivery, now) => delivery.Start(now),
            "arrive-at-drop-off" => (delivery, now) => delivery.ArriveAtDropOff(now),
            "complete" => (delivery, now) => delivery.Complete(now),
            _ => null,
        };
        return action is null
            ? Task.FromResult(Invalid<DeliveryResponse>(DeliveryErrorCodes.InvalidRequest))
            : Mutate(actor, deliveryId, operation, request, idempotencyKey, request.ConcurrencyStamp, true, action, null, ct);
    }

    public async Task<DeliveryOperationResult<DeliveryResponse>> SubmitProofAsync(DeliveryActor actor, Guid deliveryId, SubmitDeliveryProofRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (!Enum.IsDefined((DeliveryProofType)request.Type)) return Invalid<DeliveryResponse>(DeliveryErrorCodes.InvalidRequest);
        DeliveryProofType type = (DeliveryProofType)request.Type;
        if (type is DeliveryProofType.Photo or DeliveryProofType.Signature)
        {
            if (!request.MediaAssetId.HasValue) return Invalid<DeliveryResponse>(DeliveryErrorCodes.MediaInvalid);
            MediaAssetReference? asset = await media.FindAsync(request.MediaAssetId.Value, ct);
            if (asset is null || !asset.IsReady || asset.IsDeleted) return Invalid<DeliveryResponse>(DeliveryErrorCodes.MediaInvalid);
        }

        bool invalidPin = false;
        Action<DeliveryAggregate, DateTime> action = type switch
        {
            DeliveryProofType.Pin => (delivery, now) =>
            {
                bool valid = request.Pin is not null && delivery.PinHash is not null && delivery.PinSalt is not null && pins.Verify(request.Pin, delivery.PinHash, delivery.PinSalt);
                invalidPin = !valid;
                delivery.RecordPinAttempt(valid, now);
            }
            ,
            DeliveryProofType.Photo or DeliveryProofType.Signature => (delivery, now) => delivery.AddMediaProof(type, request.MediaAssetId!.Value, now),
            DeliveryProofType.RecipientName => (delivery, now) => delivery.AddRecipientName(request.RecipientName ?? string.Empty, now),
            _ => throw new InvalidOperationException("Unsupported proof type."),
        };
        DeliveryOperationResult<DeliveryResponse> result = await Mutate(actor, deliveryId, "proof:" + request.Type, request, idempotencyKey, request.ConcurrencyStamp, true, action, null, ct);
        return invalidPin && result.Status == DeliveryOperationStatus.Success ? new(DeliveryOperationStatus.Unprocessable, null, DeliveryErrorCodes.PinInvalid) : result;
    }

    public Task<DeliveryOperationResult<DeliveryResponse>> ReportFailedAsync(DeliveryActor actor, Guid deliveryId, ReportFailedDeliveryRequest request, string idempotencyKey, CancellationToken ct)
    {
        if (!Enum.IsDefined((DeliveryFailureReason)request.Reason)) return Task.FromResult(Invalid<DeliveryResponse>(DeliveryErrorCodes.InvalidRequest));
        DeliveryFailureReason reason = (DeliveryFailureReason)request.Reason;
        return Mutate(actor, deliveryId, "fail", request, idempotencyKey, request.ConcurrencyStamp, true, (delivery, now) => delivery.Fail(reason, request.Notes, now), reason.ToString(), ct);
    }

    private async Task<DeliveryOperationResult<DeliveryResponse>> Mutate(DeliveryActor actor, Guid deliveryId, string operation, object request, string idempotencyKey, Guid expectedStamp, bool requireDriverOwnership, Action<DeliveryAggregate, DateTime> action, string? reasonCode, CancellationToken ct)
    {
        if (!TryRequest(actor, idempotencyKey, request, out string keyHash, out string requestHash)) return Invalid<DeliveryResponse>(DeliveryErrorCodes.InvalidRequest);
        DeliveryIdempotencyResult? duplicate = await repository.FindIdempotencyAsync(actor.UserId, operation, keyHash, ct);
        if (duplicate is not null)
        {
            if (duplicate.RequestHash != requestHash || duplicate.DeliveryId != deliveryId) return Conflict<DeliveryResponse>(DeliveryErrorCodes.IdempotencyConflict);
            DeliveryAggregate? previous = await SafeGet(deliveryId, true, ct);
            if (previous is null) return Conflict<DeliveryResponse>(DeliveryErrorCodes.IdempotencyConflict);
            if (request is SubmitDeliveryProofRequest { Type: (short)DeliveryProofType.Pin } && !previous.Proofs.Any(x => x.Type == DeliveryProofType.Pin))
                return new(DeliveryOperationStatus.Unprocessable, null, DeliveryErrorCodes.PinInvalid);
            return Success(Map(previous));
        }

        DeliveryAggregate? delivery = await SafeGet(deliveryId, false, ct);
        if (delivery is null) return NotFound<DeliveryResponse>();
        if (requireDriverOwnership)
        {
            DriverEligibilitySnapshot? driver = await operationalDrivers.GetByUserAsync(actor.UserId, ct);
            if (driver is null || delivery.DriverId != driver.DriverId) return NotFound<DeliveryResponse>();
        }
        if (delivery.ConcurrencyStamp != expectedStamp) return Conflict<DeliveryResponse>(DeliveryErrorCodes.ConcurrencyConflict);

        DeliveryStatus oldStatus = delivery.Status;
        DateTime now = clock.UtcNow;
        try { action(delivery, now); }
        catch (DomainException ex)
        {
            string code = ex.Message.Contains("proof", StringComparison.OrdinalIgnoreCase) ? DeliveryErrorCodes.ProofIncomplete : ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase) ? DeliveryErrorCodes.PinLocked : DeliveryErrorCodes.InvalidTransition;
            return new(DeliveryOperationStatus.Unprocessable, null, code);
        }

        List<IIntegrationEvent> events = [];
        if (delivery.Status != oldStatus && delivery.DriverId.HasValue)
            events.Add(new DeliveryStatusChangedIntegrationEvent(Guid.NewGuid(), 1, delivery.Id.Value, delivery.OrderId, delivery.DriverId.Value, (short)oldStatus, (short)delivery.Status, now));
        if (operation == "assign") events.Add(new DeliveryDriverAssignedIntegrationEvent(Guid.NewGuid(), 1, delivery.Id.Value, delivery.OrderId, delivery.DriverId!.Value, now));
        if (operation == "complete") events.Add(new DeliveryCompletedIntegrationEvent(Guid.NewGuid(), 1, delivery.Id.Value, delivery.OrderId, delivery.DriverId!.Value, now));
        if (operation == "fail") events.Add(new DeliveryFailedIntegrationEvent(Guid.NewGuid(), 1, delivery.Id.Value, delivery.OrderId, delivery.DriverId!.Value, (short)delivery.FailureReason!.Value, now));
        DeliveryAuditEntry audit = new(actor.UserId, delivery.Id, operation, oldStatus, delivery.Status, now, actor.CorrelationId, keyHash, reasonCode);
        return await repository.SaveOperationAsync(delivery, actor.UserId, operation, keyHash, requestHash, audit, events, ct)
            ? Success(Map(delivery))
            : Conflict<DeliveryResponse>(DeliveryErrorCodes.ConcurrencyConflict);
    }

    private async Task<DeliveryOperationResult<DeliveryCreatedResponse>?> DuplicateCreated(Guid actorId, string keyHash, string requestHash, CancellationToken ct)
    {
        DeliveryIdempotencyResult? duplicate = await repository.FindIdempotencyAsync(actorId, "create", keyHash, ct);
        if (duplicate is null) return null;
        if (duplicate.RequestHash != requestHash) return Conflict<DeliveryCreatedResponse>(DeliveryErrorCodes.IdempotencyConflict);
        DeliveryAggregate? delivery = await repository.GetAsync(new DeliveryId(duplicate.DeliveryId), true, ct);
        return delivery is null ? Conflict<DeliveryCreatedResponse>(DeliveryErrorCodes.IdempotencyConflict) : new(DeliveryOperationStatus.Success, new(Map(delivery), null));
    }

    private async Task<DeliveryAggregate?> SafeGet(Guid id, bool noTracking, CancellationToken ct)
    {
        try { return await repository.GetAsync(new DeliveryId(id), noTracking, ct); }
        catch (DomainException) { return null; }
    }

    private static bool TryRequest(DeliveryActor actor, string key, object request, out string keyHash, out string requestHash)
    {
        keyHash = string.Empty; requestHash = string.Empty;
        if (actor.UserId == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > DeliveryRules.IdempotencyKeyMaximumLength) return false;
        keyHash = Hash(key.Trim()); requestHash = Hash(JsonSerializer.Serialize(request, request.GetType(), JsonOptions)); return true;
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static DeliveryOperationResult<T> Success<T>(T value) => new(DeliveryOperationStatus.Success, value);
    private static DeliveryOperationResult<T> NotFound<T>() => new(DeliveryOperationStatus.NotFound, default, DeliveryErrorCodes.NotFound);
    private static DeliveryOperationResult<T> Invalid<T>(string code) => new(DeliveryOperationStatus.Invalid, default, code);
    private static DeliveryOperationResult<T> Conflict<T>(string code) => new(DeliveryOperationStatus.Conflict, default, code);

    private static DeliveryResponse Map(DeliveryAggregate x) => new(
        x.Id.Value, x.OrderId, x.CustomerId, x.MerchantId, x.BranchId, x.DriverId, (short)x.Status, (short)x.ProofRequirements,
        x.CreatedAtUtc, x.UpdatedAtUtc, x.AssignedAtUtc, x.ArrivedAtPickupAtUtc, x.PickedUpAtUtc, x.StartedAtUtc,
        x.ArrivedAtDropOffAtUtc, x.DeliveredAtUtc, x.FailedAtUtc, x.FailureReason.HasValue ? (short?)x.FailureReason.Value : null,
        x.FailureNotes, x.ConcurrencyStamp,
        new(x.Pickup.Address, x.Pickup.ContactName, x.Pickup.PhoneNumber, null, x.Pickup.Instructions, x.Pickup.Latitude, x.Pickup.Longitude),
        new(x.DropOff.Address, x.DropOff.RecipientName, x.DropOff.PhoneNumber, x.DropOff.Floor, x.DropOff.Instructions, x.DropOff.Latitude, x.DropOff.Longitude),
        x.StatusHistory.OrderBy(h => h.ChangedAtUtc).ThenBy(h => h.Id.Value).Select(h => new DeliveryStatusHistoryResponse(h.Id.Value, h.PreviousStatus.HasValue ? (short?)h.PreviousStatus.Value : null, (short)h.NewStatus, (short)h.Source, h.ChangedAtUtc, h.ReasonCode, h.ReasonText)).ToArray(),
        x.Proofs.OrderBy(p => p.SubmittedAtUtc).ThenBy(p => p.Id.Value).Select(p => new DeliveryProofResponse(p.Id.Value, (short)p.Type, p.MediaAssetId, p.RecipientName, p.SubmittedAtUtc)).ToArray());
}
