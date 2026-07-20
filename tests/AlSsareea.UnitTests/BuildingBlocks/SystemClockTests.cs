using AlSsareea.BuildingBlocks.Infrastructure;

namespace AlSsareea.UnitTests.BuildingBlocks;

public sealed class SystemClockTests
{
    [Fact]
    public void UtcNowReturnsCurrentUtcTime()
    {
        var clock = new SystemClock();
        DateTime before = DateTime.UtcNow;

        DateTime actual = clock.UtcNow;

        DateTime after = DateTime.UtcNow;
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.InRange(actual, before, after);
    }
}
