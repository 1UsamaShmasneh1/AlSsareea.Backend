using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Dispatching.Domain;

namespace AlSsareea.UnitTests.Dispatching;

public sealed class DispatchingDomainTests
{
    private static readonly DateTime Now = new(2026, 8, 28, 10, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void ScoringIsDeterministicAndRewardsDistanceEtaLoadAndFairness()
    {
        CandidateScoreInput baseline = new(1_000, 300, 0, 2, Now.AddHours(-24), 300, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        CandidateScore first = DispatchScoringPolicy.Score(baseline, Now); CandidateScore second = DispatchScoringPolicy.Score(baseline, Now); Assert.Equal(first, second);
        Assert.True(first.Score > DispatchScoringPolicy.Score(baseline with { DistanceMeters = 10_000 }, Now).Score);
        Assert.True(first.Score > DispatchScoringPolicy.Score(baseline with { EtaSeconds = 2_000 }, Now).Score);
        Assert.True(first.Score > DispatchScoringPolicy.Score(baseline with { CurrentLoad = 1 }, Now).Score);
        Assert.True(first.Score > DispatchScoringPolicy.Score(baseline with { LastAssignmentAtUtc = Now.AddMinutes(-10) }, Now).Score);
    }
    [Fact]
    public void SequentialOfferDeclineMovesToNextCandidateAndNeverHasTwoActiveOffers()
    {
        DispatchRequest request = Create(); Guid firstDriver = Guid.Parse("00000000-0000-0000-0000-000000000001"); Guid secondDriver = Guid.Parse("00000000-0000-0000-0000-000000000002");
        request.StartAttempt([Candidate(request, firstDriver, 95, 1), Candidate(request, secondDriver, 90, 2)], 3, Now);
        DispatchOffer first = Assert.Single(request.Offers); Assert.Equal(firstDriver, first.DriverId); request.Decline(first.Id.Value, firstDriver, "busy", Now.AddSeconds(5), TimeSpan.FromSeconds(30));
        Assert.Equal(DispatchOfferStatus.Declined, first.Status); DispatchOffer active = Assert.Single(request.Offers, x => x.Status == DispatchOfferStatus.Pending); Assert.Equal(secondDriver, active.DriverId);
    }
    [Fact]
    public void ExpiredDeclinedAndCancelledOffersCannotBeAccepted()
    {
        DispatchRequest expired = Create(); Guid driver = Guid.NewGuid(); expired.StartAttempt([Candidate(expired, driver, 90, 1)], 3, Now); Guid offer = expired.Offers.Single().Id.Value; Assert.Throws<DomainException>(() => expired.Accept(offer, driver, Now.AddMinutes(1)));
        DispatchRequest declined = Create(); driver = Guid.NewGuid(); declined.StartAttempt([Candidate(declined, driver, 90, 1)], 3, Now); offer = declined.Offers.Single().Id.Value; declined.Decline(offer, driver, null, Now.AddSeconds(1), TimeSpan.FromSeconds(30)); Assert.Throws<DomainException>(() => declined.Accept(offer, driver, Now.AddSeconds(2)));
        DispatchRequest cancelled = Create(); driver = Guid.NewGuid(); cancelled.StartAttempt([Candidate(cancelled, driver, 90, 1)], 3, Now); offer = cancelled.Offers.Single().Id.Value; cancelled.Cancel("cancel", Now.AddSeconds(1)); Assert.Throws<DomainException>(() => cancelled.Accept(offer, driver, Now.AddSeconds(2)));
    }
    [Fact]
    public void OneWinnerOnlyAndDuplicateWinnerIsIdempotent()
    {
        DispatchRequest request = Create(); Guid driver = Guid.NewGuid(); request.StartAttempt([Candidate(request, driver, 90, 1)], 3, Now); Guid offer = request.Offers.Single().Id.Value; request.Accept(offer, driver, Now.AddSeconds(1)); request.Accept(offer, driver, Now.AddSeconds(2)); Assert.Equal(driver, request.AssignedDriverId); Assert.Equal(DispatchStatus.Assigned, request.Status); Assert.Throws<DomainException>(() => request.ManualAssign(Guid.NewGuid(), Guid.NewGuid(), "override", Now.AddSeconds(3)));
    }
    [Fact]
    public void MaximumAttemptsStopsUnboundedRetry()
    {
        DispatchRequest request = Create(); request.StartAttempt([], 1, Now); Assert.Equal(DispatchStatus.Failed, request.Status); request.StartAttempt([], 1, Now.AddMinutes(1)); Assert.Equal(1, request.AttemptNumber); Assert.Equal("maximum_attempts_exhausted", request.FailureReason);
    }
    private static DispatchRequest Create() => DispatchRequest.Create(DispatchRequestId.New(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 31.7, 35.2, null, null, Now);
    private static DispatchCandidate Candidate(DispatchRequest request, Guid driver, decimal score, int rank) => DispatchCandidate.Create(request.Id, driver, 1, 1_000, 300, 0, 2, null, score, rank, "test", Now);
}
