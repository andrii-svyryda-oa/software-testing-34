using Domain.Customers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Domain.Customers;

public class CustomerTests
{
    private static Customer NewBronze() =>
        Customer.Register(CustomerId.New(), "Alice", "alice@x.io", "+380501112233", DateTime.UtcNow);

    [Fact]
    public void Register_initialises_balances_and_tier()
    {
        var c = NewBronze();
        c.TotalPoints.Should().Be(0);
        c.TotalEarnedPoints.Should().Be(0);
        c.TierLevel.Should().Be(TierLevel.Bronze);
    }

    [Fact]
    public void Earn_on_bronze_applies_no_multiplier_and_increments_total()
    {
        var c = NewBronze();
        var awarded = c.Earn(basePoints: 500, at: DateTime.UtcNow);

        awarded.Should().Be(500);
        c.TotalPoints.Should().Be(500);
        c.TotalEarnedPoints.Should().Be(500);
        c.TierLevel.Should().Be(TierLevel.Bronze);
    }

    [Fact]
    public void Earn_on_gold_applies_1_5x_multiplier()
    {
        var c = NewBronze();
        c.AwardBonus(6_000, DateTime.UtcNow);

        var awarded = c.Earn(1_000, DateTime.UtcNow);

        awarded.Should().Be(1_500);
        c.TotalEarnedPoints.Should().Be(7_500);
        c.TierLevel.Should().Be(TierLevel.Gold);
    }

    [Fact]
    public void Earn_on_platinum_applies_2x_multiplier()
    {
        var c = NewBronze();
        c.AwardBonus(11_000, DateTime.UtcNow);

        var awarded = c.Earn(1_000, DateTime.UtcNow);

        awarded.Should().Be(2_000);
        c.TierLevel.Should().Be(TierLevel.Platinum);
    }

    [Fact]
    public void Earn_promotes_tier_when_threshold_crossed_during_call()
    {
        var c = NewBronze();
        c.AwardBonus(900, DateTime.UtcNow);

        var awarded = c.Earn(200, DateTime.UtcNow);

        awarded.Should().Be(200);
        c.TotalEarnedPoints.Should().Be(1_100);
        c.TierLevel.Should().Be(TierLevel.Silver);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Earn_rejects_non_positive(int basePoints)
    {
        var c = NewBronze();
        Action act = () => c.Earn(basePoints, DateTime.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Redeem_decrements_total_points_only()
    {
        var c = NewBronze();
        c.AwardBonus(2_000, DateTime.UtcNow);

        c.Redeem(500);

        c.TotalPoints.Should().Be(1_500);
        c.TotalEarnedPoints.Should().Be(2_000);
        c.TierLevel.Should().Be(TierLevel.Silver);
    }

    [Fact]
    public void Redeem_throws_when_insufficient_balance()
    {
        var c = NewBronze();
        c.AwardBonus(100, DateTime.UtcNow);

        Action act = () => c.Redeem(101);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Redeem_rejects_non_positive(int amount)
    {
        var c = NewBronze();
        c.AwardBonus(100, DateTime.UtcNow);

        Action act = () => c.Redeem(amount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExpirePoints_does_not_lower_tier()
    {
        var c = NewBronze();
        c.AwardBonus(11_000, DateTime.UtcNow);

        c.ExpirePoints(10_000);

        c.TotalPoints.Should().Be(1_000);
        c.TotalEarnedPoints.Should().Be(11_000);
        c.TierLevel.Should().Be(TierLevel.Platinum);
    }

    [Fact]
    public void AwardBonus_raises_tier_when_threshold_crossed()
    {
        var c = NewBronze();
        c.AwardBonus(5_000, DateTime.UtcNow);
        c.TierLevel.Should().Be(TierLevel.Gold);
    }
}
