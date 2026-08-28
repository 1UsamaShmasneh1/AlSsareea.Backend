using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Dispatching.Domain;

public sealed class DispatchRequest : AggregateRoot<DispatchRequestId>
{
    private readonly List<DispatchCandidate> _candidates = [];
    private readonly List<DispatchOffer> _offers = [];
    private readonly List<DispatchHistory> _history = [];
    private DispatchRequest() : base(default) { }
    private DispatchRequest(DispatchRequestId id, Guid deliveryId, Guid orderId, Guid merchantId, Guid? branchId, Guid zoneId, double pickupLatitude, double pickupLongitude, short? vehicleType, int? preparationSeconds, DateTime now) : base(id)
    {
        if (id.Value == Guid.Empty || deliveryId == Guid.Empty || orderId == Guid.Empty || merchantId == Guid.Empty || zoneId == Guid.Empty) throw new DomainException("Dispatch identifiers are required.");
        RequireUtc(now); DeliveryId = deliveryId; OrderId = orderId; MerchantId = merchantId; MerchantBranchId = branchId; ZoneId = zoneId; PickupLatitude = pickupLatitude; PickupLongitude = pickupLongitude; RequiredVehicleType = vehicleType; PreparationSeconds = preparationSeconds; Status = DispatchStatus.Pending; AttemptNumber = 0; CreatedAtUtc = UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); AddHistory(DispatchHistoryType.Created, now, null, null, null);
    }
    public Guid DeliveryId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid? MerchantBranchId { get; private set; }
    public Guid ZoneId { get; private set; }
    public double PickupLatitude { get; private set; }
    public double PickupLongitude { get; private set; }
    public short? RequiredVehicleType { get; private set; }
    public int? PreparationSeconds { get; private set; }
    public DispatchStatus Status { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public IReadOnlyCollection<DispatchCandidate> Candidates => _candidates.AsReadOnly();
    public IReadOnlyCollection<DispatchOffer> Offers => _offers.AsReadOnly();
    public IReadOnlyCollection<DispatchHistory> History => _history.AsReadOnly();
    public static DispatchRequest Create(DispatchRequestId id, Guid deliveryId, Guid orderId, Guid merchantId, Guid? branchId, Guid zoneId, double pickupLatitude, double pickupLongitude, short? vehicleType, int? preparationSeconds, DateTime now) => new(id, deliveryId, orderId, merchantId, branchId, zoneId, pickupLatitude, pickupLongitude, vehicleType, preparationSeconds, now);

    public void StartAttempt(IEnumerable<DispatchCandidate> candidates, int maximumAttempts, DateTime now)
    {
        if (Status is DispatchStatus.Assigned or DispatchStatus.Cancelled) throw new DomainException("Dispatch is already terminal.");
        if (AttemptNumber >= maximumAttempts) { Fail("maximum_attempts_exhausted", now); return; }
        ExpireOrCancelActive(now, true); AttemptNumber++; Status = DispatchStatus.Searching; FailureReason = null;
        DispatchCandidate[] ordered = candidates.OrderByDescending(x => x.Score).ThenBy(x => x.DriverId).Take(DispatchRules.MaximumCandidates).ToArray();
        foreach (DispatchCandidate candidate in ordered) _candidates.Add(candidate);
        AddHistory(DispatchHistoryType.AttemptStarted, now, null, null, $"attempt={AttemptNumber}"); AddHistory(DispatchHistoryType.CandidatesEvaluated, now, null, null, $"count={ordered.Length}"); Touch(now);
        if (ordered.Length == 0) { Fail("no_eligible_candidates", now); return; }
        CreateNextOffer(now, TimeSpan.FromSeconds(30));
    }

    public DispatchOffer? CreateNextOffer(DateTime now, TimeSpan duration)
    {
        if (Status is DispatchStatus.Assigned or DispatchStatus.Cancelled or DispatchStatus.Failed) return null;
        ExpireOrCancelActive(now, false);
        HashSet<Guid> offered = _offers.Where(x => x.AttemptNumber == AttemptNumber).Select(x => x.DriverId).ToHashSet();
        DispatchCandidate? next = _candidates.Where(x => x.AttemptNumber == AttemptNumber && !offered.Contains(x.DriverId)).OrderBy(x => x.Rank).FirstOrDefault();
        if (next is null) { Fail("candidates_exhausted", now); return null; }
        DispatchOffer offer = DispatchOffer.Create(DispatchOfferId.New(), Id, next.DriverId, AttemptNumber, _offers.Count + 1, now, now.Add(duration)); _offers.Add(offer); Status = DispatchStatus.Offering; AddHistory(DispatchHistoryType.OfferCreated, now, offer.Id.Value, offer.DriverId, null); Touch(now); return offer;
    }

    public bool Decline(Guid offerId, Guid driverId, string? reason, DateTime now, TimeSpan nextOfferDuration)
    {
        DispatchOffer offer = OwnedOffer(offerId, driverId); offer.Decline(reason, now); AddHistory(DispatchHistoryType.OfferDeclined, now, offer.Id.Value, driverId, reason); Touch(now); return CreateNextOffer(now, nextOfferDuration) is not null;
    }

    public void Accept(Guid offerId, Guid driverId, DateTime now)
    {
        if (Status == DispatchStatus.Assigned) { if (AssignedDriverId == driverId) return; throw new DomainException("Dispatch already has a winner."); }
        DispatchOffer offer = OwnedOffer(offerId, driverId); offer.Accept(now); AssignedDriverId = driverId; Status = DispatchStatus.Assigned; CompletedAtUtc = now;
        foreach (DispatchOffer other in _offers.Where(x => x.Id != offer.Id && x.Status == DispatchOfferStatus.Pending)) other.Supersede(now);
        AddHistory(DispatchHistoryType.OfferAccepted, now, offer.Id.Value, driverId, null); AddHistory(DispatchHistoryType.Assigned, now, offer.Id.Value, driverId, null); Touch(now);
    }

    public void ManualAssign(Guid driverId, Guid actorId, string reason, DateTime now)
    {
        if (driverId == Guid.Empty || actorId == Guid.Empty || string.IsNullOrWhiteSpace(reason)) throw new DomainException("Manual assignment requires driver, actor, and reason.");
        if (Status == DispatchStatus.Assigned) { if (AssignedDriverId == driverId) return; throw new DomainException("Dispatch already has a winner."); }
        if (Status == DispatchStatus.Cancelled) throw new DomainException("Cancelled dispatch cannot be assigned.");
        ExpireOrCancelActive(now, true); AssignedDriverId = driverId; Status = DispatchStatus.Assigned; CompletedAtUtc = now; AddHistory(DispatchHistoryType.ManualAssignment, now, null, driverId, $"actor={actorId};reason={Trim(reason)}"); AddHistory(DispatchHistoryType.Assigned, now, null, driverId, null); Touch(now);
    }
    public void Cancel(string? reason, DateTime now) { if (Status == DispatchStatus.Assigned) throw new DomainException("Assigned dispatch cannot be cancelled."); if (Status == DispatchStatus.Cancelled) return; ExpireOrCancelActive(now, true); Status = DispatchStatus.Cancelled; FailureReason = Trim(reason); CompletedAtUtc = now; AddHistory(DispatchHistoryType.Cancelled, now, null, null, FailureReason); Touch(now); }
    public void Fail(string reason, DateTime now) { if (Status is DispatchStatus.Assigned or DispatchStatus.Cancelled) throw new DomainException("Terminal dispatch cannot fail."); ExpireOrCancelActive(now, true); Status = DispatchStatus.Failed; FailureReason = Trim(reason); CompletedAtUtc = now; AddHistory(DispatchHistoryType.Failed, now, null, null, FailureReason); Touch(now); }
    public bool ExpireActive(DateTime now, TimeSpan nextDuration) { DispatchOffer? active = _offers.SingleOrDefault(x => x.Status == DispatchOfferStatus.Pending); if (active is null || active.ExpiresAtUtc > now) return false; active.Expire(now); AddHistory(DispatchHistoryType.OfferExpired, now, active.Id.Value, active.DriverId, null); Touch(now); _ = CreateNextOffer(now, nextDuration); return true; }
    private DispatchOffer OwnedOffer(Guid offerId, Guid driverId) => _offers.SingleOrDefault(x => x.Id.Value == offerId && x.DriverId == driverId) ?? throw new DomainException("Offer was not found for this driver.");
    private void ExpireOrCancelActive(DateTime now, bool cancel) { foreach (DispatchOffer offer in _offers.Where(x => x.Status == DispatchOfferStatus.Pending)) { if (offer.ExpiresAtUtc <= now) { offer.Expire(now); AddHistory(DispatchHistoryType.OfferExpired, now, offer.Id.Value, offer.DriverId, null); } else if (cancel) offer.Cancel(now); } }
    private void AddHistory(DispatchHistoryType type, DateTime now, Guid? offerId, Guid? driverId, string? detail) => _history.Add(DispatchHistory.Create(DispatchHistoryId.New(), Id, AttemptNumber, type, offerId, driverId, detail, now));
    private void Touch(DateTime now) { RequireUtc(now); UpdatedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, DispatchRules.MaximumReasonLength)];
    private static void RequireUtc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new DomainException("Timestamp must be UTC."); }
}

public sealed class DispatchCandidate : Entity<DispatchCandidateId>
{
    private DispatchCandidate() : base(default) { }
    private DispatchCandidate(DispatchCandidateId id, DispatchRequestId requestId, Guid driverId, int attempt, long distance, int eta, int load, int capacity, DateTime? lastAssignment, decimal score, int rank, string explanation, DateTime now) : base(id) { DispatchRequestId = requestId; DriverId = driverId; AttemptNumber = attempt; DistanceMeters = distance; EtaSeconds = eta; CurrentLoad = load; MaximumCapacity = capacity; LastAssignmentAtUtc = lastAssignment; Score = score; Rank = rank; Explanation = explanation; CreatedAtUtc = now; }
    public DispatchRequestId DispatchRequestId { get; private set; }
    public Guid DriverId { get; private set; }
    public int AttemptNumber { get; private set; }
    public long DistanceMeters { get; private set; }
    public int EtaSeconds { get; private set; }
    public int CurrentLoad { get; private set; }
    public int MaximumCapacity { get; private set; }
    public DateTime? LastAssignmentAtUtc { get; private set; }
    public decimal Score { get; private set; }
    public int Rank { get; private set; }
    public string Explanation { get; private set; } = string.Empty; public DateTime CreatedAtUtc { get; private set; }
    public static DispatchCandidate Create(DispatchRequestId requestId, Guid driverId, int attempt, long distance, int eta, int load, int capacity, DateTime? lastAssignment, decimal score, int rank, string explanation, DateTime now) => new(DispatchCandidateId.New(), requestId, driverId, attempt, distance, eta, load, capacity, lastAssignment, score, rank, explanation, now);
}

public sealed class DispatchOffer : Entity<DispatchOfferId>
{
    private DispatchOffer() : base(default) { }
    private DispatchOffer(DispatchOfferId id, DispatchRequestId requestId, Guid driverId, int attempt, int sequence, DateTime offered, DateTime expires) : base(id) { DispatchRequestId = requestId; DriverId = driverId; AttemptNumber = attempt; Sequence = sequence; Status = DispatchOfferStatus.Pending; OfferedAtUtc = offered; ExpiresAtUtc = expires; ConcurrencyStamp = Guid.NewGuid(); }
    public DispatchRequestId DispatchRequestId { get; private set; }
    public Guid DriverId { get; private set; }
    public int AttemptNumber { get; private set; }
    public int Sequence { get; private set; }
    public DispatchOfferStatus Status { get; private set; }
    public DateTime OfferedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public string? DeclineReason { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    internal static DispatchOffer Create(DispatchOfferId id, DispatchRequestId requestId, Guid driverId, int attempt, int sequence, DateTime offered, DateTime expires) { if (expires <= offered) throw new DomainException("Offer expiry must follow creation."); return new(id, requestId, driverId, attempt, sequence, offered, expires); }
    public void Accept(DateTime now) { EnsurePending(); if (now >= ExpiresAtUtc) { Expire(now); throw new DomainException("Expired offer cannot be accepted."); } Status = DispatchOfferStatus.Accepted; RespondedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    public void Decline(string? reason, DateTime now) { EnsurePending(); if (now >= ExpiresAtUtc) { Expire(now); throw new DomainException("Expired offer cannot be declined."); } Status = DispatchOfferStatus.Declined; DeclineReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, DispatchRules.MaximumReasonLength)]; RespondedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    internal void Expire(DateTime now) { EnsurePending(); Status = DispatchOfferStatus.Expired; RespondedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    internal void Cancel(DateTime now) { EnsurePending(); Status = DispatchOfferStatus.Cancelled; RespondedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    internal void Supersede(DateTime now) { EnsurePending(); Status = DispatchOfferStatus.Superseded; RespondedAtUtc = now; ConcurrencyStamp = Guid.NewGuid(); }
    private void EnsurePending() { if (Status != DispatchOfferStatus.Pending) throw new DomainException("Offer is no longer active."); }
}

public sealed class DispatchHistory : Entity<DispatchHistoryId>
{
    private DispatchHistory() : base(default) { }
    private DispatchHistory(DispatchHistoryId id, DispatchRequestId requestId, int attempt, DispatchHistoryType type, Guid? offerId, Guid? driverId, string? detail, DateTime occurred) : base(id) { DispatchRequestId = requestId; AttemptNumber = attempt; Type = type; OfferId = offerId; DriverId = driverId; Detail = detail; OccurredAtUtc = occurred; }
    public DispatchRequestId DispatchRequestId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DispatchHistoryType Type { get; private set; }
    public Guid? OfferId { get; private set; }
    public Guid? DriverId { get; private set; }
    public string? Detail { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    internal static DispatchHistory Create(DispatchHistoryId id, DispatchRequestId requestId, int attempt, DispatchHistoryType type, Guid? offerId, Guid? driverId, string? detail, DateTime occurred) => new(id, requestId, attempt, type, offerId, driverId, detail, occurred);
}
