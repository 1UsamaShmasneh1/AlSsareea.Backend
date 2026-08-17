using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Tracking.Domain;

namespace AlSsareea.UnitTests.Tracking;

public sealed class TrackingDomainTests
{
    [Theory]
    [InlineData(-90, -180)]
    [InlineData(0, 0)]
    [InlineData(90, 180)]
    public void CoordinatesAcceptValidBounds(double latitude, double longitude) => _ = LocationPosition.Create(latitude, longitude);

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    [InlineData(double.NaN, 0)]
    public void CoordinatesRejectInvalidValues(double latitude, double longitude) => Assert.Throws<DomainException>(() => LocationPosition.Create(latitude, longitude));

    [Theory]
    [InlineData(0.0, null, null)]
    [InlineData(-1.0, null, null)]
    [InlineData(1.0, -0.1, null)]
    [InlineData(1.0, null, -0.1)]
    [InlineData(1.0, null, 360.0)]
    public void MeasurementsRejectInvalidValues(double accuracy, double? speed, double? heading) => Assert.Throws<DomainException>(() => Create(1, accuracy, speed, heading));

    [Theory]
    [InlineData(1.0, null, null)]
    [InlineData(1.0, 0.0, 0.0)]
    [InlineData(250.0, 15.5, 359.9)]
    public void MeasurementsAcceptValidValues(double accuracy, double? speed, double? heading) => _ = Create(1, accuracy, speed, heading);

    [Fact]
    public void TimestampsMustBeUtc() => Assert.Throws<DomainException>(() => DriverLocation.Create(DriverLocationId.New(), Guid.NewGuid(), LocationPosition.Create(0, 0), DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local), DateTime.UtcNow, 1, null, null, null, 1, LocationSource.Live));

    [Fact]
    public void SequenceCannotBeNegative() => Assert.Throws<DomainException>(() => Create(-1));

    [Fact]
    public void LatestUsesSequenceThenTimestampAndNeverMovesBackward()
    {
        DateTime now = DateTime.UtcNow; DriverLocation first = Create(10, recorded: now); DriverLatestLocation latest = DriverLatestLocation.Create(first, now);
        Assert.False(latest.TryPromote(Create(9, recorded: now.AddSeconds(1)), now.AddSeconds(1)));
        Assert.False(latest.TryPromote(Create(10, recorded: now), now.AddSeconds(1)));
        Assert.True(latest.TryPromote(Create(11, recorded: now.AddSeconds(2)), now.AddSeconds(2)));
        Assert.Equal(11, latest.LastSequenceNumber);
    }

    [Fact]
    public void MovementPlausibilityAllowsAccuracyJitterAndRejectsImpossibleJump()
    {
        LocationPosition origin = LocationPosition.Create(31.7683, 35.2137);
        Assert.True(MovementPlausibility.IsPlausible(origin, LocationPosition.Create(31.7684, 35.2138), TimeSpan.FromSeconds(5), 10, 10, 75));
        Assert.False(MovementPlausibility.IsPlausible(origin, LocationPosition.Create(32.0853, 34.7818), TimeSpan.FromSeconds(10), 5, 5, 75));
        Assert.True(MovementPlausibility.IsPlausible(origin, LocationPosition.Create(31.7685, 35.2139), TimeSpan.FromSeconds(1), 50, 50, 10));
    }

    private static DriverLocation Create(long sequence, double accuracy = 5, double? speed = null, double? heading = null, DateTime? recorded = null) => DriverLocation.Create(DriverLocationId.New(), Guid.Parse("c5288ca8-cdf1-4bc3-a3e3-b4f2190d7850"), LocationPosition.Create(31.7683, 35.2137), recorded ?? DateTime.UtcNow, DateTime.UtcNow, accuracy, speed, heading, null, sequence, LocationSource.Live);
}
