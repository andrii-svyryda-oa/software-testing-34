# Task 04 — Application: Customers (register / earn / redeem)

## Goal

Implement the three core customer use cases: register a customer, earn points (with tier multiplier), and redeem points against a reward (atomically: validate balance, decrement reward stock, append a `Redeemed` transaction, decrement customer balance).

## Files to add

```
Application/Customers/Commands/RegisterCustomerCommand.cs
Application/Customers/Commands/RegisterCustomerCommandValidator.cs
Application/Customers/Commands/EarnPointsCommand.cs
Application/Customers/Commands/EarnPointsCommandValidator.cs
Application/Customers/Commands/RedeemPointsCommand.cs
Application/Customers/Commands/RedeemPointsCommandValidator.cs
Application/Customers/Queries/GetCustomerByIdQuery.cs
```

Each command file contains the `record` request **and** its `IRequestHandler` (matches reference convention). The validator goes in the sibling file.

## RegisterCustomerCommand

```csharp
public record RegisterCustomerCommand : IRequest<Result<Customer, CustomerException>>
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
```

Handler logic:

1. `customerRepository.GetByEmail(request.Email)` → if `Some`, return `CustomerAlreadyExistsException`.
2. Else `Customer.Register(CustomerId.New(), name, email, phone, joinDate: DateTime.UtcNow)`.
3. `customerRepository.Add(...)`. Wrap unknown exceptions as `CustomerUnknownException(CustomerId.Empty(), ex)`.

Validator:

- `Name`: not empty, 3..255 chars.
- `Email`: not empty, valid email, max 255 chars.
- `Phone`: not empty, regex `^\+?[0-9\s\-()]{7,20}$`.

## EarnPointsCommand

```csharp
public record EarnPointsCommand : IRequest<Result<Customer, CustomerException>>
{
    public required Guid CustomerId { get; init; }
    public required int BasePoints { get; init; }
    public string Description { get; init; } = "Points earned";
}
```

Handler logic:

1. Load customer by id; `None` → `CustomerNotFoundException`.
2. Capture `at = DateTime.UtcNow`.
3. `var awarded = customer.Earn(request.BasePoints, at);` — domain method applies multiplier and updates `TotalEarnedPoints` + `TierLevel`.
4. Atomically (single `SaveChanges`): persist updated customer + new `PointTransaction.Earned(...)` record with `points = awarded`. Use a single repository call or a unit-of-work pattern — the reference project saves through each repo's `Update`/`Add`; we accept that pattern here too. Document on the handler that "atomicity is delegated to the EF Core change tracker; both writes occur in one `SaveChangesAsync` because the same `DbContext` is scoped per request".

Validator:

- `CustomerId`: not empty.
- `BasePoints`: `> 0`, `<= 1_000_000`.
- `Description`: max 500 chars.

## RedeemPointsCommand

```csharp
public record RedeemPointsCommand : IRequest<Result<Customer, CustomerException>>
{
    public required Guid CustomerId { get; init; }
    public required Guid RewardId { get; init; }
}
```

Handler logic:

1. Load customer → `None` → `CustomerNotFoundException`.
2. Load reward → `None` → `RedeemRewardNotFoundException(customerId, rewardId)`.
3. `if (!reward.IsActive)` → `RedeemRewardInactiveException`.
4. `if (reward.StockQuantity == 0)` → `RedeemRewardOutOfStockException`.
5. `if (customer.TotalPoints < reward.PointsCost)` → `InsufficientPointsException(customerId, reward.PointsCost, customer.TotalPoints)`.
6. Mutate domain: `customer.Redeem(reward.PointsCost); reward.Decrement();`
7. Append `PointTransaction.Redeemed(..., points: reward.PointsCost, description: $"Redeemed: {reward.Name}", at: DateTime.UtcNow)`.
8. Persist all three (customer update + reward update + transaction add) in one `SaveChangesAsync`.
9. Wrap unknown exceptions as `CustomerUnknownException`.

Validator:

- `CustomerId`: not empty.
- `RewardId`: not empty.

## GetCustomerByIdQuery

```csharp
public record GetCustomerByIdQuery(Guid CustomerId) : IRequest<Option<Customer>>;
```

Handler delegates to `ICustomerQueries.GetById(new CustomerId(request.CustomerId), ct)`. Returning `Option<Customer>` keeps the read flow lightweight and lets the controller map `None → 404` directly.

## Concurrency note (write into the handler XML doc on `RedeemPointsCommand`)

The naive implementation has a race condition under concurrent redemptions of a scarce reward: two requests both read `StockQuantity = 1` and both proceed to decrement. There are three acceptable fixes — pick **one** and apply it consistently:

1. **EF Core concurrency token** (`[Timestamp]` / `xmin` rowversion). Add `xmin` as a concurrency token on `Reward` in the EF configuration (task 07). Repository `Update` lets `DbUpdateConcurrencyException` propagate, and the handler retries up to N times.
2. **Database-level lock**: `context.Rewards.FromSqlRaw("SELECT * FROM rewards WHERE id = {0} FOR UPDATE", id)` inside a transaction.
3. **Serializable transaction**: open a `using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable)` in the handler; commit on success, retry on serialization failure.

Default choice: **option 1** (concurrency token) — it has the lowest blast radius and is testable. Document the choice in the handler's XML comment.

## Acceptance criteria

- All commands return `Result<Customer, CustomerException>`; the query returns `Option<Customer>`. No unhandled exceptions for known business errors.
- Validators reject all invalid inputs listed above.
- `dotnet build` is clean.
- The codebase still has no `Microsoft.EntityFrameworkCore` reference outside `Infrastructure`.

## Verification

Manual sanity checks (no automated tests yet — task 10/11):

1. Construct an in-memory fake `ICustomerRepository`, register a customer, call `EarnPointsCommand` with `basePoints = 1000`. After 5 calls (totalling 5000 base), the returned customer is in tier `Gold`. After the 6th call (`basePoints = 1000`), `awarded` is `1500` (Gold multiplier) and `TotalEarnedPoints == 6500`.
2. Same customer with `TotalPoints = 5000` redeeming a reward of cost `5001` returns `InsufficientPointsException`.

## Out of scope

- DTO-level validators (task 08).
- Persistence (task 07).
- Tests (tasks 10–11).

## Commit message

```
Task 04: customer use cases (register / earn / redeem)

Adds RegisterCustomerCommand, EarnPointsCommand (with tier multiplier),
RedeemPointsCommand (validates balance + reward stock, decrements both),
and GetCustomerByIdQuery. Concurrency on reward stock uses an EF
concurrency token (configured in task 07).
```
