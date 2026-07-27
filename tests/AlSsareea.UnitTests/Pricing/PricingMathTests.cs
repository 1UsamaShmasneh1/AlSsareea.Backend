using AlSsareea.BuildingBlocks.Domain;
using AlSsareea.Modules.Pricing.Domain;

namespace AlSsareea.UnitTests.Pricing;

public sealed class PricingMathTests
{
    [Theory]
    [InlineData(0, 500, 0)]
    [InlineData(1, 5_000, 1)]
    [InlineData(5, 1_000, 1)]
    [InlineData(10_005, 1_000, 1_001)]
    [InlineData(100, 10_000, 100)]
    public void PercentageUsesIntegerHalfUpRounding(long amount, int basisPoints, long expected) =>
        Assert.Equal(expected, PricingMath.Percentage(amount, basisPoints));

    [Fact]
    public void ArithmeticDetectsOverflow() =>
        Assert.Throws<OverflowException>(() => PricingMath.Add(long.MaxValue, 1));

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void InvalidPercentageIsRejected(int basisPoints) =>
        Assert.Throws<DomainException>(() => PricingMath.Percentage(100, basisPoints));

    [Fact]
    public void CapsAreAppliedDeterministically()
    {
        Assert.Equal(50, PricingMath.Cap(20, 50, 100));
        Assert.Equal(100, PricingMath.Cap(120, 50, 100));
        Assert.Equal(75, PricingMath.Cap(75, 50, 100));
    }
}
