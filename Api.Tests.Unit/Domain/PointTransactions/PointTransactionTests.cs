using Domain.Customers;
using Domain.PointTransactions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Domain.PointTransactions;

public class PointTransactionTests
{
    [Fact]
    public void Earned_isExpired_true_at_exactly_12_months()
    {
        var at = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t = PointTransaction.Earned(PointTransactionId.New(), CustomerId.New(), 100, "x", at);

        t.IsExpired(at.AddMonths(12)).Should().BeTrue();
        t.IsExpired(at.AddMonths(12).AddMilliseconds(-1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(PointTransactionType.Redeemed)]
    [InlineData(PointTransactionType.Expired)]
    public void Redeemed_or_expired_isExpired_always_false(PointTransactionType type)
    {
        var at = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t = type == PointTransactionType.Redeemed
            ? PointTransaction.Redeemed(PointTransactionId.New(), CustomerId.New(), 50, "r", at)
            : PointTransaction.Expired(PointTransactionId.New(), CustomerId.New(), 50, "e", at);

        t.IsExpired(at.AddYears(50)).Should().BeFalse();
    }

    [Fact]
    public void Earned_initialises_remaining_to_points()
    {
        var t = PointTransaction.Earned(PointTransactionId.New(), CustomerId.New(), 200, "x", DateTime.UtcNow);
        t.Remaining.Should().Be(200);
    }

    [Fact]
    public void Bonus_initialises_remaining_to_points()
    {
        var t = PointTransaction.Bonus(PointTransactionId.New(), CustomerId.New(), 200, "x", DateTime.UtcNow);
        t.Remaining.Should().Be(200);
    }

    [Fact]
    public void Redeemed_has_zero_remaining()
    {
        var t = PointTransaction.Redeemed(PointTransactionId.New(), CustomerId.New(), 200, "x", DateTime.UtcNow);
        t.Remaining.Should().Be(0);
    }

    [Fact]
    public void Consume_reduces_remaining()
    {
        var t = PointTransaction.Earned(PointTransactionId.New(), CustomerId.New(), 200, "x", DateTime.UtcNow);
        t.Consume(50);
        t.Remaining.Should().Be(150);
    }

    [Fact]
    public void Consume_throws_when_amount_exceeds_remaining()
    {
        var t = PointTransaction.Earned(PointTransactionId.New(), CustomerId.New(), 100, "x", DateTime.UtcNow);
        Action act = () => t.Consume(101);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Consume_throws_for_redeemed_transactions()
    {
        var t = PointTransaction.Redeemed(PointTransactionId.New(), CustomerId.New(), 100, "x", DateTime.UtcNow);
        Action act = () => t.Consume(10);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Factories_reject_non_positive_points(int points)
    {
        Action act = () => PointTransaction.Earned(
            PointTransactionId.New(), CustomerId.New(), points, "x", DateTime.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
