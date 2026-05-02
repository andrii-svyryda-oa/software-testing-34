# Task 12 — Database tests (Testcontainers — consistency & concurrency)

## Goal

Verify low-level database guarantees that are *not* visible from a single HTTP test: balance consistency under concurrent earn/redeem, transaction-history integrity (FKs, ordering), and the optimistic-concurrency token on `Reward` correctly prevents over-redemption under load.

These tests share the `IntegrationTestWebFactory` from task 09 but talk to the `DbContext` (and sometimes raw SQL) directly — they don't go through HTTP.

## Files to add

```
Api.Tests.Integrations/Database/PointBalanceConsistencyTests.cs
Api.Tests.Integrations/Database/TransactionHistoryIntegrityTests.cs
Api.Tests.Integrations/Database/RewardStockConcurrencyTests.cs
Api.Tests.Integrations/Database/SeedingTests.cs
```

## Required test cases

### `PointBalanceConsistencyTests`

1. **`Customer_total_points_matches_transaction_sum`**:
   Seed a customer + 50 mixed transactions (earned/bonus/redeemed/expired) with explicit amounts via `LoyaltySeeder`. Assert:
   `customer.TotalPoints == sum(Earned + Bonus) − sum(Redeemed + Expired)`.

2. **`Customer_total_earned_points_matches_earned_plus_bonus_sum`** — never decreases.

3. **`Concurrent_earn_calls_preserve_consistency`**:
   - Pre-seed 1 customer.
   - Open 20 parallel `IServiceScope`s; each scope sends `EarnPointsCommand{ basePoints=10 }`.
   - After all 20 complete, expect `customer.TotalPoints == sum(awarded)` exactly (no lost updates).
   - Number of `PointTransaction` rows of type `Earned` for this customer == 20.
   - Recompute multiplier factors per call to detect interleaving issues.

   This test exposes any non-atomic increment in `EarnPointsCommand` — handler should re-read inside the scope (no shared in-memory state).

### `TransactionHistoryIntegrityTests`

1. **`Foreign_key_cascade_on_customer_delete`**:
   Insert customer + 5 transactions. `Remove(customer)` → expect transactions removed. (Validates the EF `OnDelete(DeleteBehavior.Cascade)` config.)

2. **`Inserting_transaction_with_unknown_customer_id_fails`** — assert `DbUpdateException` referencing the FK constraint name (`fk_point_transactions_customers_id`).

3. **`History_query_orders_newest_first`**: insert 10 transactions with monotonically increasing `CreatedAt`; `GetHistoryFor` returns them in reverse chronological order.

### `RewardStockConcurrencyTests`

The killer test for the redeem flow. Setup:

- 1 customer with `TotalPoints = 100_000` (more than enough).
- 1 reward with `PointsCost = 100, StockQuantity = 5`.

Test body:

```csharp
var tasks = Enumerable.Range(0, 20)
    .Select(_ => Task.Run(async () =>
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        try
        {
            await sender.Send(new RedeemPointsCommand { CustomerId = customerId, RewardId = rewardId });
            return true;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }))
    .ToArray();

var results = await Task.WhenAll(tasks);
```

Assertions:

1. **Exactly 5** of the 20 attempts result in a successful redemption (because stock was 5).
2. The DB shows `reward.StockQuantity == 0`.
3. `customer.TotalPoints` decreased by exactly `5 * 100 = 500`.
4. Exactly 5 `PointTransaction` rows of type `Redeemed` exist for this customer-reward pair.
5. **No** stock value below zero ever appears in any historical row (sanity check via raw SQL `SELECT MIN(stock_quantity) FROM rewards WHERE id = $1`).

This test fails if the EF concurrency token (configured in task 07) is missing or if the redeem handler does not check stock atomically. If you used isolation-level transactions instead of optimistic concurrency, results 1 still holds, but failed attempts may surface as `PostgresException` with code `40001` (serialization failure) — accept either.

### `SeedingTests`

1. **`Seeder_produces_at_least_10000_records`**:
   Apply `LoyaltySeeder.SeedAsync(context, targetTotal: 10_000, seed: 42)`. Assert `Customers.Count() + Rewards.Count() + PointTransactions.Count() >= 10_000`.

2. **`Seeder_is_internally_consistent`**:
   For each seeded customer, `TotalPoints == sum(Earned+Bonus) − sum(Redeemed+Expired)` and `TotalEarnedPoints == sum(Earned+Bonus)`.

3. **`Seeder_is_idempotent`**: calling it twice yields the same total counts.

4. **`Seeder_is_deterministic_with_fixed_seed`**:
   Two fresh DBs seeded with `seed: 42` produce identical aggregate sums (`SUM(points)` per type). This guards against accidentally introducing wall-clock time dependencies.

## Acceptance criteria

- All four test files run green locally with Docker available.
- The concurrency test from `RewardStockConcurrencyTests` produces stable results across 10 consecutive runs (target: 0 flakes — if you see any, fix the handler, not the test).
- Total `Database/` runtime < 60 s per file (Postgres container startup is the dominant cost — keep test classes small so they share a single `IClassFixture<IntegrationTestWebFactory>`).

## Verification

Run the concurrency test ten times in a row:

```bash
for i in {1..10}; do
  dotnet test Api.Tests.Integrations \
    --filter "FullyQualifiedName~RewardStockConcurrencyTests" \
    || break
done
```

## Out of scope

- Performance tests (task 13).
- Postgres-specific tuning (just defaults are fine).

## Commit message

```
Task 12: database consistency and concurrency tests

PointBalanceConsistency, TransactionHistoryIntegrity (FK cascade,
ordering), RewardStockConcurrency (20 parallel redeems against
stock=5 yields exactly 5 successes via the EF concurrency token),
and Seeding tests (≥10k records, internally consistent, idempotent,
deterministic with fixed seed).
```
