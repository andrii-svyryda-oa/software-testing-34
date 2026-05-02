# Task 10 — Unit tests

## Goal

Cover the pure-domain logic with fast, deterministic xUnit tests: tier calculation, point multiplier, expiration rule, and the entity-level invariants on `Customer`, `Reward`, and `PointTransaction`. No I/O, no DI, no mocks for domain code.

Target metrics:

- ≥ 90 % line coverage on the `Domain/` project.
- All tests run in < 1 s combined.

## Files to add

```
Api.Tests.Unit/Api.Tests.Unit.csproj                       # ensure refs Tests.Common (or Test.Data + Domain directly)
Api.Tests.Unit/Domain/Customers/TierLevelsTests.cs
Api.Tests.Unit/Domain/Customers/CustomerTests.cs
Api.Tests.Unit/Domain/Rewards/RewardTests.cs
Api.Tests.Unit/Domain/PointTransactions/PointTransactionTests.cs
```

## NuGet packages — `Api.Tests.Unit.csproj`

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
<PackageReference Include="FluentAssertions" Version="7.0.0" />
<PackageReference Include="AutoFixture.Xunit2" Version="4.18.1" />
<PackageReference Include="coverlet.collector" Version="6.0.0" />
```

References only `Domain` (and `Test.Data` if you want to share builders); does **not** need `Tests.Common`.

## Test cases

### `TierLevelsTests.cs`

```csharp
[Theory]
[InlineData(0,     TierLevel.Bronze)]
[InlineData(999,   TierLevel.Bronze)]
[InlineData(1_000, TierLevel.Silver)]
[InlineData(4_999, TierLevel.Silver)]
[InlineData(5_000, TierLevel.Gold)]
[InlineData(9_999, TierLevel.Gold)]
[InlineData(10_000, TierLevel.Platinum)]
[InlineData(50_000, TierLevel.Platinum)]
public void FromTotalEarnedPoints_returns_correct_tier(int earned, TierLevel expected) =>
    TierLevels.FromTotalEarnedPoints(earned).Should().Be(expected);

[Theory]
[InlineData(TierLevel.Bronze,   1.0)]
[InlineData(TierLevel.Silver,   1.0)]
[InlineData(TierLevel.Gold,     1.5)]
[InlineData(TierLevel.Platinum, 2.0)]
public void MultiplierFor_returns_correct_factor(TierLevel tier, double expected) =>
    TierLevels.MultiplierFor(tier).Should().Be((decimal)expected);

[Theory]
[InlineData(   0, TierLevel.Silver,   1_000)]
[InlineData( 800, TierLevel.Silver,     200)]
[InlineData(2500, TierLevel.Gold,     2_500)]
[InlineData(7000, TierLevel.Platinum, 3_000)]
[InlineData(99_999, TierLevel.Platinum, 0)]
public void ProgressFrom_reports_correct_next_and_distance(int earned, TierLevel next, int toNext)
{
    var (n, d) = TierLevels.ProgressFrom(earned);
    n.Should().Be(next);
    d.Should().Be(toNext);
}
```

### `CustomerTests.cs`

Business-rule fields are set explicitly:

```csharp
[Fact]
public void Earn_on_bronze_applies_no_multiplier_and_increments_total()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    var awarded = c.Earn(basePoints: 500, at: DateTime.UtcNow);

    awarded.Should().Be(500);
    c.TotalPoints.Should().Be(500);
    c.TotalEarnedPoints.Should().Be(500);
    c.TierLevel.Should().Be(TierLevel.Bronze);
}

[Fact]
public void Earn_on_gold_applies_1_5x_multiplier()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    c.AwardBonus(6_000, DateTime.UtcNow);  // forces Gold tier with no multiplier

    var awarded = c.Earn(1_000, DateTime.UtcNow);

    awarded.Should().Be(1_500);
    c.TotalEarnedPoints.Should().Be(7_500);
    c.TierLevel.Should().Be(TierLevel.Gold);
}

[Fact]
public void Earn_on_platinum_applies_2x_multiplier()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    c.AwardBonus(11_000, DateTime.UtcNow);  // Platinum

    var awarded = c.Earn(1_000, DateTime.UtcNow);

    awarded.Should().Be(2_000);
    c.TierLevel.Should().Be(TierLevel.Platinum);
}

[Fact]
public void Earn_promotes_tier_when_threshold_crossed_during_call()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    c.AwardBonus(900, DateTime.UtcNow); // still Bronze; multiplier 1×

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
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    Action act = () => c.Earn(basePoints, DateTime.UtcNow);
    act.Should().Throw<ArgumentOutOfRangeException>();
}

[Fact]
public void Redeem_decrements_total_points_only()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    c.AwardBonus(2_000, DateTime.UtcNow);

    c.Redeem(500);

    c.TotalPoints.Should().Be(1_500);
    c.TotalEarnedPoints.Should().Be(2_000);
    c.TierLevel.Should().Be(TierLevel.Silver);  // tier unchanged
}

[Fact]
public void Redeem_throws_when_insufficient_balance()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    c.AwardBonus(100, DateTime.UtcNow);
    Action act = () => c.Redeem(101);
    act.Should().Throw<InvalidOperationException>();
}

[Fact]
public void ExpirePoints_does_not_lower_tier()
{
    var c = Customer.Register(CustomerId.New(), "A", "a@x.io", "+38", DateTime.UtcNow);
    c.AwardBonus(11_000, DateTime.UtcNow);  // Platinum

    c.ExpirePoints(10_000);

    c.TotalPoints.Should().Be(1_000);
    c.TotalEarnedPoints.Should().Be(11_000);  // never decreases
    c.TierLevel.Should().Be(TierLevel.Platinum);
}
```

### `RewardTests.cs`

```csharp
[Fact]
public void Decrement_throws_when_out_of_stock()
{
    var r = Reward.Create(RewardId.New(), "x", "y", 100, "cat", stockQuantity: 0);
    Action act = () => r.Decrement();
    act.Should().Throw<InvalidOperationException>();
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
public void Create_rejects_negative_pointsCost()
{
    Action act = () => Reward.Create(RewardId.New(), "x", "y", -1, "cat", 1);
    act.Should().Throw<ArgumentOutOfRangeException>();
}
```

### `PointTransactionTests.cs`

```csharp
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
    PointTransaction t = type == PointTransactionType.Redeemed
        ? PointTransaction.Redeemed(PointTransactionId.New(), CustomerId.New(), 50, "r", at)
        : PointTransaction.Expired(PointTransactionId.New(), CustomerId.New(), 50, "e", at);
    t.IsExpired(at.AddYears(50)).Should().BeFalse();
}
```

## Acceptance criteria

- `dotnet test Api.Tests.Unit` passes (≥ ~25 test cases, including theory rows).
- Combined runtime < 1 s.
- Coverage report (collected via `coverlet.collector`) shows ≥ 90 % line coverage on `Domain/`.

## Out of scope

- Handler-level unit tests (covered indirectly by integration tests in task 11; if you want, add a small set with NSubstitute, but the reference project doesn't and we won't either).

## Commit message

```
Task 10: unit tests for domain logic

Covers TierLevels (thresholds + multiplier + progress), Customer
(earn/redeem/expire/award-bonus invariants), Reward (stock + active
guards), and PointTransaction.IsExpired with explicit boundary tests.
~25 cases, sub-second runtime, ≥90% coverage on Domain.
```
