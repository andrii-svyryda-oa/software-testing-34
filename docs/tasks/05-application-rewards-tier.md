# Task 05 — Application: Rewards & Tier query

## Goal

Implement reward management (list available, create) and the customer-tier endpoint that reports current tier, total earned points, and progress toward the next tier.

## Files to add

```
Application/Rewards/Commands/CreateRewardCommand.cs
Application/Rewards/Commands/CreateRewardCommandValidator.cs
Application/Rewards/Queries/GetAvailableRewardsQuery.cs
Application/Customers/Queries/GetCustomerTierQuery.cs
```

## CreateRewardCommand

```csharp
public record CreateRewardCommand : IRequest<Result<Reward, RewardException>>
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required int PointsCost { get; init; }
    public required string Category { get; init; }
    public required int StockQuantity { get; init; }
}
```

Handler:

1. `Reward.Create(RewardId.New(), name, description, pointsCost, category, stockQuantity)` — domain validates non-positive cost and negative stock.
2. `rewardRepository.Add(...)`.
3. Wrap unknowns as `RewardUnknownException(RewardId.Empty(), ex)`.

Validator:

- `Name`: not empty, 3..255 chars.
- `Description`: not empty, max 1000 chars.
- `PointsCost`: `> 0`, `<= 1_000_000`.
- `Category`: not empty, max 50 chars.
- `StockQuantity`: `>= 0`.

## GetAvailableRewardsQuery

```csharp
public record GetAvailableRewardsQuery : IRequest<IReadOnlyList<Reward>>;
```

Handler returns `await rewardQueries.GetAvailable(ct)` — `IsActive == true && StockQuantity > 0`.

> The spec endpoint is `GET /api/rewards`. We return only **available** rewards (active + in stock) by default. If a separate "show inactive too" view is needed later, add a query parameter; not now.

## GetCustomerTierQuery

```csharp
public record CustomerTierResult
{
    public required TierLevel Current      { get; init; }
    public required TierLevel Next         { get; init; }
    public required int TotalEarnedPoints  { get; init; }
    public required int PointsToNext       { get; init; }
}

public record GetCustomerTierQuery(Guid CustomerId)
    : IRequest<Result<CustomerTierResult, CustomerException>>;
```

Handler:

1. Load customer via `ICustomerQueries.GetById`. `None` → `CustomerNotFoundException`.
2. Compute progress: `var (next, pointsToNext) = TierLevels.ProgressFrom(customer.TotalEarnedPoints);`.
3. Return:

```csharp
new CustomerTierResult
{
    Current = customer.TierLevel,
    Next = next,
    TotalEarnedPoints = customer.TotalEarnedPoints,
    PointsToNext = pointsToNext,  // 0 when already Platinum
}
```

No validator needed (the controller validates the route param).

## Acceptance criteria

- `dotnet build` is clean.
- `GetAvailableRewardsQuery` returns *only* active rewards with stock; verified manually with a fake `IRewardQueries`.
- `GetCustomerTierQuery` for a customer with `TotalEarnedPoints = 7500` returns `Current = Gold, Next = Platinum, PointsToNext = 2500`.
- For a Platinum customer (`TotalEarnedPoints = 12000`), `PointsToNext = 0` and `Next = Platinum`.

## Out of scope

- DTOs / controller wiring (task 08).
- Stock concurrency (handled in task 04 / task 07).
- Tests (task 11 covers these endpoints).

## Commit message

```
Task 05: rewards CRUD (subset) and customer-tier query

Adds CreateRewardCommand, GetAvailableRewardsQuery, and
GetCustomerTierQuery returning current/next tier + points-to-next
progress.
```
