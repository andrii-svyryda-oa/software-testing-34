using Domain.Rewards;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Domain.Rewards;

public class RewardTests
{
    [Fact]
    public void Decrement_throws_when_out_of_stock()
    {
        var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", stockQuantity: 0);
        Action act = () => r.Decrement();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decrement_reduces_stock_by_one()
    {
        var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", stockQuantity: 3);
        r.Decrement();
        r.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void IsAvailable_false_when_inactive()
    {
        var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", 5);
        r.Deactivate();
        r.IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_false_when_stock_zero()
    {
        var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", 0);
        r.IsAvailable().Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_true_when_active_and_stocked()
    {
        var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", 5);
        r.IsAvailable().Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_pointsCost(int cost)
    {
        Action act = () => Reward.Create(RewardId.New(), "x", "y", cost, "cat", 1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_rejects_negative_stock()
    {
        Action act = () => Reward.Create(RewardId.New(), "x", "y", 100, "cat", -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Activate_after_Deactivate_restores_active_flag()
    {
        var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", 5);
        r.Deactivate();
        r.IsActive.Should().BeFalse();
        r.Activate();
        r.IsActive.Should().BeTrue();
    }
}
