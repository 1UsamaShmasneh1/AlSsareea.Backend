using AlSsareea.BuildingBlocks.Contracts;

namespace AlSsareea.Modules.Dispatching.Contracts;

public static class DispatchingPermissions
{
    public const string Read = "dispatching.requests.read";
    public const string Start = "dispatching.requests.start";
    public const string Manage = "dispatching.requests.manage";
    public const string ManualAssign = "dispatching.requests.manual_assign";
    public const string Cancel = "dispatching.requests.cancel";
}

public sealed record StartDispatchRequest(Guid DeliveryId, Guid ZoneId, short? RequiredVehicleType, int? PreparationSeconds);
public sealed record OfferDecisionRequest(string? Reason);
public sealed record RetryDispatchRequest(string? Reason);
public sealed record CancelDispatchRequest(string? Reason);
public sealed record ManualAssignDispatchRequest(Guid DriverId, string Reason);
public sealed record DispatchCandidateResponse(Guid Id, Guid DriverId, long DistanceMeters, int EtaSeconds, int CurrentLoad, int MaximumCapacity, decimal Score, int Rank, string Explanation, DateTime CreatedAtUtc);
public sealed record DispatchOfferResponse(Guid Id, Guid DriverId, int Sequence, short Status, DateTime OfferedAtUtc, DateTime ExpiresAtUtc, DateTime? RespondedAtUtc, string? DeclineReason);
public sealed record DispatchHistoryResponse(Guid Id, int AttemptNumber, short Type, Guid? OfferId, Guid? DriverId, string? Detail, DateTime OccurredAtUtc);
public sealed record DispatchResponse(Guid Id, Guid DeliveryId, Guid OrderId, Guid MerchantId, Guid? MerchantBranchId, Guid ZoneId, short Status, int AttemptNumber, Guid? AssignedDriverId, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? CompletedAtUtc, string? FailureReason, Guid ConcurrencyStamp, IReadOnlyList<DispatchCandidateResponse> Candidates, IReadOnlyList<DispatchOfferResponse> Offers, IReadOnlyList<DispatchHistoryResponse> History);

public sealed record DispatchRequestedIntegrationEvent(Guid Id, int Version, Guid DispatchRequestId, Guid DeliveryId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DispatchOfferCreatedIntegrationEvent(Guid Id, int Version, Guid DispatchRequestId, Guid OfferId, Guid DriverId, DateTime ExpiresAtUtc, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DispatchOfferAcceptedIntegrationEvent(Guid Id, int Version, Guid DispatchRequestId, Guid OfferId, Guid DriverId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DriverAssignedIntegrationEvent(Guid Id, int Version, Guid DispatchRequestId, Guid DeliveryId, Guid DriverId, DateTime OccurredAtUtc) : IIntegrationEvent;
public sealed record DispatchFailedIntegrationEvent(Guid Id, int Version, Guid DispatchRequestId, Guid DeliveryId, string Reason, DateTime OccurredAtUtc) : IIntegrationEvent;
