# Task 09 — Test infrastructure (`Tests.Common` + `Test.Data`)

## Goal

Stand up everything the test projects need before any test exists: an `IntegrationTestWebFactory` running PostgreSQL via Testcontainers, a `BaseIntegrationTest` with shared `HttpClient` + `DbContext`, AutoFixture customizations, entity builders, and a `LoyaltySeeder` that produces ≥ 10 000 records distributed across the three aggregates.

## Files to add

```
Tests.Common/Tests.Common.csproj                            # add NuGet packages
Tests.Common/IntegrationTestWebFactory.cs                   # WebApplicationFactory<Program> + Testcontainers
Tests.Common/BaseIntegrationTest.cs                         # IClassFixture<IntegrationTestWebFactory>
Tests.Common/TestsExtensions.cs                             # ToResponseModel<T>, etc.
Tests.Common/Auth/TestAuthHandler.cs                        # only if any endpoint requires auth
Test.Data/Test.Data.csproj                                  # add NuGet packages
Test.Data/AutoFixture/LoyaltyCustomization.cs               # ICustomization for Customer/Reward/PointTransaction
Test.Data/Builders/CustomersData.cs
Test.Data/Builders/RewardsData.cs
Test.Data/Builders/PointTransactionsData.cs
Test.Data/Seeders/LoyaltySeeder.cs                          # populates DbContext with ≥10k records
Api/appsettings.test.json                                   # neutral config consumed by IntegrationTestWebFactory
```

## NuGet packages

`Tests.Common.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="coverlet.collector" Version="6.0.0" />
```

`Test.Data.csproj`:

```xml
<PackageReference Include="AutoFixture" Version="4.18.1" />
<PackageReference Include="AutoFixture.Xunit2" Version="4.18.1" />
<PackageReference Include="Bogus" Version="35.6.0" />
```

(Both AutoFixture and Bogus are listed because the spec mentions either is acceptable. Use Bogus where AutoFixture's auto-generation is insufficient — e.g. realistic-looking phone numbers / categories.)

## `IntegrationTestWebFactory`

Copy `Z:\UNIK\ASP_NET_REST_API_PROJECT\Tests.Common\TestFactory.cs` and adapt:

```csharp
public class IntegrationTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("loyalty_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveServiceByType(typeof(DbContextOptions<ApplicationDbContext>));

            var dataSource = new NpgsqlDataSourceBuilder(_db.GetConnectionString()).Build();
            services.AddDbContext<ApplicationDbContext>(o => o
                .UseNpgsql(dataSource, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
        }).ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile("appsettings.test.json").AddEnvironmentVariables();
        });
    }

    public Task InitializeAsync() => _db.StartAsync();
    public new Task DisposeAsync() => _db.DisposeAsync().AsTask();
}

public static class TestFactoryExtensions
{
    public static void RemoveServiceByType(this IServiceCollection services, Type t)
    {
        var d = services.SingleOrDefault(s => s.ServiceType == t);
        if (d is not null) services.Remove(d);
    }
}
```

## `BaseIntegrationTest`

```csharp
public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebFactory>
{
    protected readonly ApplicationDbContext Context;
    protected readonly HttpClient Client;

    protected BaseIntegrationTest(IntegrationTestWebFactory factory)
    {
        var scope = factory.Services.CreateScope();
        Context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    protected async Task<int> SaveChangesAsync()
    {
        var result = await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return result;
    }
}
```

> Auth handlers from the reference are *not* needed unless we add auth — keep `TestAuthHandler.cs` only if endpoints require it. Per task 08, we don't.

## `TestsExtensions`

```csharp
public static class TestsExtensions
{
    public static async Task<T> ToResponseModel<T>(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content)
            ?? throw new ArgumentException("Response content cannot be null.");
    }
}
```

## `Test.Data` builders

### `CustomersData`

```csharp
public static class CustomersData
{
    private static readonly Faker Faker = new();

    public static Customer Bronze(string? email = null) =>
        Customer.Register(CustomerId.New(),
            name:  Faker.Name.FullName(),
            email: email ?? Faker.Internet.Email(),
            phone: Faker.Phone.PhoneNumber("+##########"),
            joinDate: DateTime.UtcNow.AddDays(-Faker.Random.Int(0, 365)));

    /// <summary>
    /// Returns a Customer with the requested business state (TotalEarnedPoints + tier),
    /// constructed via reflection-free path: register, then earn the exact amount in
    /// one Earn() call so multipliers don't interfere — pass a Bronze starting state.
    /// </summary>
    public static Customer WithEarned(int earned, string? email = null)
    {
        var c = Bronze(email);
        if (earned > 0) c.AwardBonus(earned, DateTime.UtcNow);  // bonus: no multiplier
        return c;
    }
}
```

### `RewardsData`

```csharp
public static class RewardsData
{
    private static readonly Faker Faker = new();

    public static Reward Catalog(int pointsCost, int stock) =>
        Reward.Create(RewardId.New(),
            name: Faker.Commerce.ProductName(),
            description: Faker.Commerce.ProductDescription(),
            pointsCost: pointsCost,
            category: Faker.Commerce.Categories(1)[0],
            stockQuantity: stock);

    public static Reward Inactive(int pointsCost = 100)
    {
        var r = Catalog(pointsCost, stock: 10);
        r.Deactivate();
        return r;
    }

    public static Reward OutOfStock(int pointsCost = 100) => Catalog(pointsCost, stock: 0);
}
```

### `PointTransactionsData`

```csharp
public static class PointTransactionsData
{
    private static readonly Faker Faker = new();

    public static PointTransaction Earned(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Earned(PointTransactionId.New(), cid, points,
            description: Faker.Commerce.Department(), at: at ?? DateTime.UtcNow);

    public static PointTransaction Redeemed(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Redeemed(PointTransactionId.New(), cid, points,
            description: $"Redemption {Faker.Random.AlphaNumeric(8)}",
            at: at ?? DateTime.UtcNow);

    public static PointTransaction Bonus(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Bonus(PointTransactionId.New(), cid, points,
            description: "Bonus", at: at ?? DateTime.UtcNow);

    public static PointTransaction Expired(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Expired(PointTransactionId.New(), cid, points,
            description: "Expired", at: at ?? DateTime.UtcNow);
}
```

## `LoyaltySeeder`

This produces the 10 000+-record dataset required by the spec for integration / perf tests. Distribution:

| Aggregate         | Count   | Notes |
|-------------------|---------|-------|
| Customer          |  500    | tiers: 70 % Bronze, 20 % Silver, 8 % Gold, 2 % Platinum |
| Reward            |   50    | 80 % active+stocked, 15 % active-no-stock, 5 % inactive; PointsCost ∈ \[100, 5000\] |
| PointTransaction  | 9 450   | per-customer 5–40 transactions; 60 % Earned, 25 % Redeemed, 10 % Bonus, 5 % Expired |
| **Total**         | 10 000  | |

API:

```csharp
public static class LoyaltySeeder
{
    public record SeedSummary(int Customers, int Rewards, int Transactions);

    public static async Task<SeedSummary> SeedAsync(
        ApplicationDbContext context,
        int targetTotal = 10_000,
        int? seed = null,
        CancellationToken ct = default)
    {
        var faker = seed is null ? new Faker() : new Faker { Random = new Randomizer(seed.Value) };
        // ... bulk-build entities, AddRangeAsync, SaveChangesAsync ...
    }
}
```

Implementation guidelines:

- Build all entities **in memory** first, then a single `AddRangeAsync` per `DbSet` and one `SaveChangesAsync` at the end. With 10 000 rows on Postgres + Testcontainers this is < 5 s on a developer laptop.
- Use a fixed `seed` (e.g. `42`) when called from deterministic tests; pass `null` for production-like randomness in perf scenarios.
- After inserting, recompute the materialized customer state (`TotalPoints`, `TotalEarnedPoints`, `TierLevel`) by *replaying* their transactions in chronological order — this guarantees the seeded customer state is internally consistent with their history. Alternatively, generate transactions to match a pre-decided customer state.
- Spread `CreatedAt` across the past 18 months (so some `Earned` transactions are >12 months old → exercises expiration logic).
- Keep the seeder **idempotent enough** to re-run on the same DB by pre-clearing tables when called.

## `Api/appsettings.test.json`

```json
{
  "Logging": { "LogLevel": { "Default": "Warning", "Microsoft.AspNetCore": "Warning" } },
  "ConnectionStrings": { "Default": "Server=localhost;Port=5432;Database=loyalty_test;User Id=postgres;Password=postgres;" }
}
```

The connection string is overridden at runtime by `IntegrationTestWebFactory`, but the file must exist so `AddJsonFile("appsettings.test.json")` doesn't fail.

## Acceptance criteria

- `dotnet build` clean.
- `Test.Data` references only `Domain` (and AutoFixture/Bogus packages).
- `Tests.Common` builds and resolves `Program` via `WebApplicationFactory<Program>` (proves `Api` exposes the `public partial class Program`).
- Manually seeded DB via the seeder reaches `Customers + Rewards + PointTransactions ≥ 10_000`.
- Re-running the seeder twice produces the same total (idempotency check).

## Out of scope

- Actual tests (tasks 10–13).

## Commit message

```
Task 09: test infrastructure (Tests.Common + Test.Data)

Adds IntegrationTestWebFactory backed by Testcontainers Postgres,
BaseIntegrationTest with shared HttpClient/DbContext, AutoFixture/Bogus
builders for Customer/Reward/PointTransaction, and LoyaltySeeder that
generates ≥10 000 internally-consistent records for integration and
performance tests.
```
