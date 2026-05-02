# Task 02 — Domain layer

## Goal

Implement the three aggregate roots (`Customer`, `PointTransaction`, `Reward`), their strongly-typed IDs, enums, and pure-function business invariants. The domain has zero framework references and is fully unit-testable.

## Files to add

```
Domain/Customers/
    CustomerId.cs            # record CustomerId(Guid Value) — New(), Empty(), ToString()
    Customer.cs              # entity + invariants
    TierLevel.cs             # enum Bronze/Silver/Gold/Platinum
    TierLevels.cs            # static class with thresholds + multiplier helpers

Domain/PointTransactions/
    PointTransactionId.cs
    PointTransaction.cs
    PointTransactionType.cs  # enum Earned/Redeemed/Expired/Bonus

Domain/Rewards/
    RewardId.cs
    Reward.cs                # entity + invariants
```

Delete the placeholder file from task 01.

## Detailed contracts

### `TierLevel` and `TierLevels`

```csharp
public enum TierLevel { Bronze, Silver, Gold, Platinum }

public static class TierLevels
{
    public const int BronzeThreshold   = 0;
    public const int SilverThreshold   = 1_000;
    public const int GoldThreshold     = 5_000;
    public const int PlatinumThreshold = 10_000;

    public static TierLevel FromTotalEarnedPoints(int totalEarned) => totalEarned switch
    {
        >= PlatinumThreshold => TierLevel.Platinum,
        >= GoldThreshold     => TierLevel.Gold,
        >= SilverThreshold   => TierLevel.Silver,
        _                    => TierLevel.Bronze,
    };

    public static decimal MultiplierFor(TierLevel tier) => tier switch
    {
        TierLevel.Platinum => 2.0m,
        TierLevel.Gold     => 1.5m,
        _                  => 1.0m,
    };

    public static (TierLevel next, int pointsToNext) ProgressFrom(int totalEarned)
    {
        if (totalEarned < SilverThreshold)   return (TierLevel.Silver,   SilverThreshold   - totalEarned);
        if (totalEarned < GoldThreshold)     return (TierLevel.Gold,     GoldThreshold     - totalEarned);
        if (totalEarned < PlatinumThreshold) return (TierLevel.Platinum, PlatinumThreshold - totalEarned);
        return (TierLevel.Platinum, 0);  // already top tier
    }
}
```

### `Customer`

Fields: `Id, Name, Email, Phone, TierLevel, TotalPoints, TotalEarnedPoints, JoinDate`.

> `TotalEarnedPoints` is **derived** from the cumulative sum of `Earned + Bonus` transactions and never decreases. It drives tier calculation. `TotalPoints` is the redeemable balance (earned + bonus − redeemed − expired). The spec lists only `TotalPoints` on the entity, but tier rules require knowing total *earned*; we keep both to make invariants enforceable on the entity. Document this in the class XML doc.

Methods:

- `public static Customer Register(CustomerId id, string name, string email, string phone, DateTime joinDate)` — sets `TierLevel = Bronze`, `TotalPoints = 0`, `TotalEarnedPoints = 0`.
- `public int Earn(int basePoints, DateTime at)` — applies tier multiplier, returns the earned amount, increments `TotalPoints` and `TotalEarnedPoints`, recomputes `TierLevel`. Throws `ArgumentOutOfRangeException` if `basePoints <= 0`. The multiplier is taken from the **current** tier (before update).
- `public void Redeem(int points)` — fails (return `false` or throw a domain exception) if `points > TotalPoints` or `points <= 0`. Decrements `TotalPoints` only; `TotalEarnedPoints` is untouched.
- `public void ExpirePoints(int points)` — same shape as `Redeem` but does not touch `TotalEarnedPoints` either; tier never falls.
- `public void AwardBonus(int points, DateTime at)` — adds bonus points without multiplier; affects both `TotalPoints` and `TotalEarnedPoints`.

Invariant: after every operation, `TotalPoints >= 0`, `TotalEarnedPoints >= 0`, `TierLevel == TierLevels.FromTotalEarnedPoints(TotalEarnedPoints)`.

### `PointTransactionType`

```csharp
public enum PointTransactionType { Earned, Redeemed, Expired, Bonus }
```

### `PointTransaction`

Immutable after construction. Fields: `Id, CustomerId, Points, Type, Description, CreatedAt`.

- `public static PointTransaction Earned(PointTransactionId id, CustomerId customerId, int points, string description, DateTime at)` — `points` must be `> 0`.
- `Redeemed(...)` — `points` must be `> 0` (stored as the absolute redeemed amount).
- `Expired(...)` — same.
- `Bonus(...)` — same.
- `public bool IsExpired(DateTime now)` — returns `Type is Earned or Bonus && now >= CreatedAt.AddMonths(12)`. Always `false` for `Redeemed` / `Expired` types (those are records of consumption, not balances).

### `Reward`

Fields: `Id, Name, Description, PointsCost, Category, StockQuantity, IsActive`.

- `public static Reward Create(RewardId id, string name, string description, int pointsCost, string category, int stockQuantity)` — `pointsCost > 0`, `stockQuantity >= 0`, `IsActive = true`.
- `public void Decrement()` — throws if `StockQuantity == 0` (define `RewardOutOfStockException` in the **Application** layer in task 03; in domain just throw `InvalidOperationException` with a descriptive message — domain doesn't depend on application exceptions).
- `public void Deactivate()` / `Activate()`.
- `public bool IsAvailable() => IsActive && StockQuantity > 0;`

## Acceptance criteria

- All types compile with no warnings.
- No `using Microsoft.*`, `using EntityFrameworkCore`, or other framework usings anywhere in `Domain/`.
- All public mutable state has `private set;` accessors.
- Each entity has exactly one factory method on the static type (`Register` / `Create` / typed `Earned`/`Redeemed`/`Expired`/`Bonus`).
- Constants `1000`, `5000`, `10000` appear only in `TierLevels.cs`.

## Verification

- `dotnet build Domain` is clean.
- Open `Domain/` in IDE — there must be **zero** non-system using directives.
- Spot-check: `Customer.Earn(500, _)` on a Bronze customer raises `TotalEarnedPoints` to 500 (no multiplier). On a Gold customer (`TotalEarnedPoints == 6000`), `Earn(1000, _)` raises by `1500`. On a Platinum customer, by `2000`.

## Out of scope

- Persistence configuration (task 07).
- Application-layer exceptions, repositories, use cases (tasks 03–06).
- Unit tests (task 10) — even though the domain is testable now, we batch all unit tests into task 10 to keep this task tightly focused on domain code.

## Commit message

```
Task 02: domain layer (Customer, PointTransaction, Reward)

Adds aggregate entities with strongly-typed IDs, enums, tier
thresholds/multipliers, and invariant-protecting methods.
Domain has zero framework dependencies and is fully unit-testable.
```
