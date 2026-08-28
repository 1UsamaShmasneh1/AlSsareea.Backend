using AlSsareea.Modules.Dispatching.Application;
using AlSsareea.Modules.Drivers.Contracts;

namespace AlSsareea.UnitTests.Dispatching;

public sealed class DispatchEligibilityTests
{
    private readonly Guid zone = Guid.NewGuid();
    [Fact] public void EligibleDriverPasses() => Assert.True(DispatchCandidateEligibility.IsEligible(Eligible(), zone, 2));
    [Fact] public void InactiveDriverIsExcluded() => Assert.False(DispatchCandidateEligibility.IsEligible(Eligible() with { IsActive = false }, zone, 2));
    [Fact] public void SuspendedDriverIsExcluded() => Assert.False(DispatchCandidateEligibility.IsEligible(Eligible() with { HasActiveSuspension = true }, zone, 2));
    [Theory][InlineData(1)][InlineData(4)][InlineData(5)] public void OfflineOrUnavailableDriverIsExcluded(short status) => Assert.False(DispatchCandidateEligibility.IsEligible(Eligible() with { AvailabilityStatus = status }, zone, 2));
    [Fact] public void WrongZoneIsExcluded() => Assert.False(DispatchCandidateEligibility.IsEligible(Eligible(), Guid.NewGuid(), 2));
    [Fact] public void WrongVehicleIsExcluded() => Assert.False(DispatchCandidateEligibility.IsEligible(Eligible(), zone, 3));
    [Fact] public void CapacityExceededIsExcluded() => Assert.False(DispatchCandidateEligibility.IsEligible(Eligible() with { CurrentLoad = 2 }, zone, 2));
    private DriverDispatchCandidateSnapshot Eligible() => new(Guid.NewGuid(), true, true, 2, 2, [zone], 2, 0, false, null);
}
