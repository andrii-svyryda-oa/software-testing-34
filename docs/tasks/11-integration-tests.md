# Task 11 — Integration tests (WebApplicationFactory)

## Goal

Verify each endpoint end-to-end against a real Postgres (Testcontainers) via `WebApplicationFactory<Program>`. Tests assert on both the HTTP response **and** the resulting DB state, mirroring the reference project's `UsersControllerTests` shape.

## Files to add

```
Api.Tests.Integrations/Customers/CustomersControllerTests.cs
Api.Tests.Integrations/Rewards/RewardsControllerTests.cs
```

## Setup pattern (every test class)

```csharp
public class CustomersControllerTests : BaseIntegrationTest, IAsyncLifetime
{
    private Customer _alice = null!;

    public CustomersControllerTests(IntegrationTestWebFactory factory) : base(factory) { }

    public async Task InitializeAsync()
    {
        _alice = CustomersData.Bronze("alice@x.io");
        await Context.Customers.AddAsync(_alice);
        await SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Context.PointTransactions.RemoveRange(Context.PointTransactions);
        Context.Customers.RemoveRange(Context.Customers);
        Context.Rewards.RemoveRange(Context.Rewards);
        await SaveChangesAsync();
    }
}
```

Each test class **owns its own data lifecycle**: arranges in `InitializeAsync`, cleans in `DisposeAsync`. Tests within a class share the seeded fixture but are otherwise independent.

## CustomersControllerTests — required cases

1. **`Register_returns_201_and_persists`**
   `POST /api/customers` with `{name:"Bob",email:"bob@x.io",phone:"+1..."}` → 201 Created, response carries Bronze tier and 0 points; DB has one extra customer.

2. **`Register_with_duplicate_email_returns_409`**

3. **`Register_with_invalid_payload_returns_400`** (missing fields, malformed email, short phone) — three Theory rows.

4. **`Get_existing_customer_returns_200_with_dto`** — uses `_alice`.

5. **`Get_nonexistent_customer_returns_404`**.

6. **`Earn_below_silver_threshold_keeps_bronze`**:
   `POST /api/customers/{alice}/earn` with `{basePoints:500}` → 200; subsequent `GET /api/customers/{alice}` shows `TotalPoints=500, TotalEarnedPoints=500, TierLevel=Bronze`.

7. **`Earn_crossing_silver_threshold_promotes_tier`**:
   Two consecutive earns of 500 + 600 → final tier `Silver`, `TotalEarnedPoints == 1100`.

8. **`Earn_uses_gold_multiplier`**:
   Pre-arrange Alice with 6000 earned (using `Customer.AwardBonus` in test setup), then `POST .../earn` with 1000 base → response shows `TotalEarnedPoints == 7500` (1000 × 1.5 added).
   Also assert exactly one new `PointTransaction` row of type `Earned` with `Points == 1500` exists for Alice.

9. **`Earn_uses_platinum_multiplier`** — analogue for 11000 earned start, base=1000, expect +2000.

10. **`Earn_with_non_positive_returns_400`** — Theory rows: 0, -10.

11. **`Redeem_succeeds_and_decrements_stock`**:
    Seed a `Reward(cost=200, stock=3)`. Pre-fill Alice with 500 points. `POST .../redeem {rewardId}` → 200; DB shows `customer.TotalPoints == 300`, `reward.StockQuantity == 2`, one `PointTransaction` of type `Redeemed` with `Points == 200`.

12. **`Redeem_insufficient_points_returns_422`** — `cost=10000, alice.TotalPoints=100` → 422 with body containing "InsufficientPoints…".

13. **`Redeem_out_of_stock_returns_422`** — reward with `stock=0`.

14. **`Redeem_inactive_reward_returns_422`** — reward `Deactivate()`d.

15. **`Redeem_unknown_reward_returns_404`** — random Guid as reward id.

16. **`History_returns_paginated_newest_first`**:
    Seed 25 transactions with explicit `CreatedAt` spread. `GET .../history?page=1&pageSize=10` returns 10 newest items; `Total == 25`. `?page=3&pageSize=10` returns the oldest 5.

17. **`Tier_returns_progress`**:
    `_alice` with `TotalEarnedPoints=2500` (set via setup) → `Current=Silver, Next=Gold, PointsToNext=2500`.

## RewardsControllerTests — required cases

1. **`List_returns_only_active_in_stock_rewards`**:
   Seed three rewards: active+stocked, active+stock=0, inactive. `GET /api/rewards` returns exactly the first.

2. **`List_orders_by_name`** (or whatever ordering you committed to in `RewardRepository.GetAvailable`).

3. **`Create_returns_201_and_persists`**.

4. **`Create_with_negative_cost_returns_400`** (DTO validator).

5. **`Create_with_zero_stock_is_allowed_and_excluded_from_list`** — verifies "out-of-stock" rewards still exist in the DB but never appear in the public list.

## Acceptance criteria

- `dotnet test Api.Tests.Integrations` passes locally (Docker required for Testcontainers).
- Tests are fully isolated: running `xunit.run` with parallel collection disabled per fixture is acceptable, but data cleanup in `DisposeAsync` must be sufficient that re-running the suite yields identical results.
- No flaky tests: redeem-stock and earn-multiplier tests must pass 10× in a row.

## Verification

```bash
dotnet test Api.Tests.Integrations -c Release --logger "console;verbosity=detailed"
```

Then run again immediately:

```bash
dotnet test Api.Tests.Integrations
```

Should still pass.

## Out of scope

- Database-only consistency / concurrency tests (task 12).
- Performance tests (task 13).

## Commit message

```
Task 11: integration tests for customer & reward endpoints

WebApplicationFactory + Testcontainers Postgres. Covers register/get,
earn (with Bronze/Gold/Platinum multiplier paths and tier promotion),
redeem (success, insufficient, out-of-stock, inactive, unknown reward),
paginated history, tier progress, and rewards list/create.
```
