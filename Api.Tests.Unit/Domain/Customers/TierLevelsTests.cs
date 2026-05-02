using Domain.Customers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Domain.Customers;

public class TierLevelsTests
{
    [Theory]
    [InlineData(0, TierLevel.Bronze)]
    [InlineData(999, TierLevel.Bronze)]
    [InlineData(1_000, TierLevel.Silver)]
    [InlineData(4_999, TierLevel.Silver)]
    [InlineData(5_000, TierLevel.Gold)]
    [InlineData(9_999, TierLevel.Gold)]
    [InlineData(10_000, TierLevel.Platinum)]
    [InlineData(50_000, TierLevel.Platinum)]
    public void FromTotalEarnedPoints_returns_correct_tier(int earned, TierLevel expected) =>
        TierLevels.FromTotalEarnedPoints(earned).Should().Be(expected);

    [Theory]
    [InlineData(TierLevel.Bronze, 1.0)]
    [InlineData(TierLevel.Silver, 1.0)]
    [InlineData(TierLevel.Gold, 1.5)]
    [InlineData(TierLevel.Platinum, 2.0)]
    public void MultiplierFor_returns_correct_factor(TierLevel tier, double expected) =>
        TierLevels.MultiplierFor(tier).Should().Be((decimal)expected);

    [Theory]
    [InlineData(0, TierLevel.Silver, 1_000)]
    [InlineData(800, TierLevel.Silver, 200)]
    [InlineData(2500, TierLevel.Gold, 2_500)]
    [InlineData(7000, TierLevel.Platinum, 3_000)]
    [InlineData(99_999, TierLevel.Platinum, 0)]
    public void ProgressFrom_reports_correct_next_and_distance(int earned, TierLevel next, int toNext)
    {
        var (n, d) = TierLevels.ProgressFrom(earned);
        n.Should().Be(next);
        d.Should().Be(toNext);
    }
}
