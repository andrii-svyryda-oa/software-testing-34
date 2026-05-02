# Task 06 — Application: Point transaction history & expiration

## Goal

Implement the customer's point-transaction history (paginated) and the deterministic expiration of points older than 12 months.

## Files to add

```
Application/PointTransactions/Queries/GetCustomerHistoryQuery.cs
Application/PointTransactions/Commands/ExpirePointsCommand.cs
Application/PointTransactions/Commands/ExpirePointsCommandValidator.cs
```

## GetCustomerHistoryQuery

```csharp
public record PaginatedResult<T>(IReadOnlyList<T> Data, int Total);

public record GetCustomerHistoryQuery(Guid CustomerId, int Skip, int Take)
    : IRequest<Result<PaginatedResult<PointTransaction>, CustomerException>>;
```

Handler:

1. Verify customer exists (`ICustomerQueries.GetById`); `None` → `CustomerNotFoundException`.
2. Run `IPointTransactionQueries.GetHistoryFor(customerId, skip, take, ct)` and `CountFor(customerId, ct)` in parallel (`Task.WhenAll`).
3. Return `PaginatedResult<PointTransaction>(data, total)`.

Defaults / clamping (in handler, before query):

- `take = Math.Clamp(request.Take, 1, 100)`
- `skip = Math.Max(0, request.Skip)`

Sort: most recent first (`ORDER BY created_at DESC`). The repository is responsible; document in the query interface XML doc.

## ExpirePointsCommand

```csharp
public record ExpirePointsCommand : IRequest<Result<int, CustomerException>>
{
    public required Guid CustomerId { get; init; }
    public required DateTime At { get; init; }   // injected, not DateTime.UtcNow — keeps handler pure
}
```

Handler logic ("FIFO" expiration, mirrors most loyalty programs):

1. Load customer; `None` → `CustomerNotFoundException`.
2. Load all *active* `Earned`/`Bonus` transactions for the customer that have a `CreatedAt` older than `request.At - 12 months` (use a dedicated repo method `GetExpirableEarnedFor(customerId, threshold, ct)`). Add this method to `IPointTransactionRepository` in this PR.
3. Compute the to-expire amount = sum(points). Cap by `customer.TotalPoints` (you cannot expire what was already redeemed). The exact rule: walk the list in oldest-first order, skipping points that are already covered by prior `Redeemed` transactions. The simplest correct algorithm:
   - Start with `unredeemed = customer.TotalPoints` (current redeemable balance).
   - `expire = 0`.
   - For each candidate transaction (oldest first), `expire += min(transaction.Points, unredeemed); unredeemed -= ...`. Stop when `unredeemed == 0`.

   This is a known correct FIFO model; alternatively define a separate "remaining" column on `PointTransaction` and update incrementally. Pick the cleaner approach for this codebase: store a `Remaining` field on `PointTransaction` for `Earned`/`Bonus` types, decremented during redemption (task 04 update — see "Coupling note" below).
4. If `expire > 0`:
   - `customer.ExpirePoints(expire)`
   - Add `PointTransaction.Expired(...)` for the aggregated amount with description `"Points expired (>12 months)"`.
   - Persist all changes in one `SaveChangesAsync`.
5. Return `expire` (the number of points expired).

Validator:

- `CustomerId`: not empty.
- `At`: `> default(DateTime)` and `<= DateTime.UtcNow.AddDays(1)` (no far-future calls).

### Coupling note (update task 04 in this PR)

If you choose the `Remaining` field approach (recommended), you must:

1. Extend `PointTransaction` (`Domain/PointTransactions/PointTransaction.cs`): add `int Remaining { get; private set; }`. For `Earned`/`Bonus` factories, `Remaining = points`. For `Redeemed`/`Expired`, `Remaining = 0`. Add `void Consume(int amount)` that throws if `amount > Remaining`.
2. Extend `RedeemPointsCommandHandler` (task 04): when redeeming `cost` points, walk active earned transactions oldest-first and call `Consume(...)` until `cost` is satisfied. This is part of THIS task's PR — adjust task 04's handler in the same commit so the `Remaining` invariant holds end-to-end.

This is the only place tasks "leak" into each other; document the leak in the PR description.

## Acceptance criteria

- `GetCustomerHistoryQuery` returns paginated history, newest first; `take` clamped to `[1, 100]`.
- `ExpirePointsCommand` is idempotent: calling it twice with the same `At` on the same customer yields `0` on the second call.
- `ExpirePointsCommand` never makes `customer.TotalPoints` negative.
- The customer's `TotalEarnedPoints` and `TierLevel` are unchanged after expiration (tier never falls).

## Out of scope

- A scheduled job that runs `ExpirePointsCommand` for all customers — for the spec, exposing it as a command is enough; a controller endpoint is optional. If exposed, gate it behind admin auth (out of spec; document in task 08).

## Commit message

```
Task 06: point-transaction history and expiration

Adds GetCustomerHistoryQuery (paginated, newest first) and
ExpirePointsCommand (FIFO expiration of points >12 months old).
Introduces PointTransaction.Remaining and updates the redeem flow
to consume earned transactions oldest-first.
```
