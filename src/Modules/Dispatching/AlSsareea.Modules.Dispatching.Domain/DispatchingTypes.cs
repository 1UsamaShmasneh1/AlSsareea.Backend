using AlSsareea.BuildingBlocks.Domain;

namespace AlSsareea.Modules.Dispatching.Domain;

public readonly record struct DispatchRequestId(Guid Value) { public static DispatchRequestId New() => new(Guid.NewGuid()); }
public readonly record struct DispatchCandidateId(Guid Value) { public static DispatchCandidateId New() => new(Guid.NewGuid()); }
public readonly record struct DispatchOfferId(Guid Value) { public static DispatchOfferId New() => new(Guid.NewGuid()); }
public readonly record struct DispatchHistoryId(Guid Value) { public static DispatchHistoryId New() => new(Guid.NewGuid()); }
public enum DispatchStatus : short { Pending = 1, Searching = 2, Offering = 3, Assigned = 4, Failed = 5, Cancelled = 6 }
public enum DispatchOfferStatus : short { Pending = 1, Accepted = 2, Declined = 3, Expired = 4, Cancelled = 5, Superseded = 6 }
public enum DispatchHistoryType : short { Created = 1, AttemptStarted = 2, CandidatesEvaluated = 3, OfferCreated = 4, OfferAccepted = 5, OfferDeclined = 6, OfferExpired = 7, Assigned = 8, Failed = 9, Cancelled = 10, ManualAssignment = 11 }
public static class DispatchRules
{
    public const int MaximumReasonLength = 500;
    public const int MaximumExplanationLength = 1000;
    public const int MaximumCandidates = 100;
    public const int IdempotencyKeyMaximumLength = 200;
}

public sealed record CandidateScoreInput(long DistanceMeters, int EtaSeconds, int CurrentLoad, int MaximumCapacity, DateTime? LastAssignmentAtUtc, int? PreparationSeconds, Guid DriverId);
public sealed record CandidateScore(decimal Score, string Explanation);

public static class DispatchScoringPolicy
{
    public const decimal DistanceWeight = 0.35m;
    public const decimal EtaWeight = 0.30m;
    public const decimal LoadWeight = 0.20m;
    public const decimal FairnessWeight = 0.10m;
    public const decimal PreparationWeight = 0.05m;

    public static CandidateScore Score(CandidateScoreInput input, DateTime now)
    {
        if (input.DistanceMeters < 0 || input.EtaSeconds < 0 || input.CurrentLoad < 0 || input.MaximumCapacity <= 0) throw new DomainException("Candidate score input is invalid.");
        decimal distance = Math.Max(0m, 1m - Math.Min(input.DistanceMeters, 20_000) / 20_000m);
        decimal eta = Math.Max(0m, 1m - Math.Min(input.EtaSeconds, 3_600) / 3_600m);
        decimal load = 1m - Math.Min(1m, input.CurrentLoad / (decimal)input.MaximumCapacity);
        decimal fairness = input.LastAssignmentAtUtc.HasValue ? Math.Min(1m, (decimal)(now - input.LastAssignmentAtUtc.Value).TotalHours / 24m) : 1m;
        decimal preparation = input.PreparationSeconds.HasValue ? Math.Max(0m, 1m - Math.Abs(input.EtaSeconds - input.PreparationSeconds.Value) / 3_600m) : 0m;
        decimal total = decimal.Round((distance * DistanceWeight + eta * EtaWeight + load * LoadWeight + fairness * FairnessWeight + preparation * PreparationWeight) * 100m, 4);
        return new(total, $"distance={distance:F4};eta={eta:F4};load={load:F4};fairness={fairness:F4};preparation={preparation:F4}");
    }
}
