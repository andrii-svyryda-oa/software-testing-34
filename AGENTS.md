# AGENTS.md — Loyalty Program API

This document is the source of truth for how this codebase is organized and how new code is written. It is distilled from the reference project at `Z:\UNIK\ASP_NET_REST_API_PROJECT` (FoodManagementSystemApi) and adapted to the requirements in `docs/task_requirement.txt` (Завдання 34: Програма лояльності).

The goal of the project is a Loyalty Program REST API (Customer / PointTransaction / Reward) implemented with Clean Architecture, ASP.NET Core 8, EF Core + PostgreSQL, MediatR, FluentValidation, fully covered by unit / integration / database / performance tests.

---

## 1. Solution layout

The reference project uses 4 production projects + 3 test projects. We mirror that structure.

```
SoftwareTesting/
├── Api/                          # ASP.NET Core Web API (composition root, controllers, DTOs)
├── Application/                  # Use cases (MediatR commands/queries, validators, interfaces, Result<T,E>)
├── Domain/                       # Entities, value-object IDs, enums, business invariants
├── Infrastructure/               # EF Core DbContext, configurations, migrations, repositories, seeders
├── Tests.Common/                 # IntegrationTestWebFactory, BaseIntegrationTest, TestAuthHandler
├── Test.Data/                    # AutoFixture/Bogus builders, sample entity factories
├── Api.Tests.Integrations/       # Integration tests (WebApplicationFactory + Testcontainers)
├── Api.Tests.Unit/               # Unit tests for domain / handler logic
├── perf/                         # k6 scripts (load / stress)
├── .github/workflows/            # CI pipeline (build + tests on push/PR)
├── docs/                         # Task descriptions (one per PR)
├── SoftwareTesting.sln
└── AGENTS.md                     # this file
```

Project references (one direction only — outer layers depend on inner ones, never the reverse):

```
Domain          ← (no refs)
Application     → Domain
Infrastructure  → Application, Domain
Api             → Application, Infrastructure
Test.Data       → Domain
Tests.Common    → Api, Application, Infrastructure, Test.Data
Api.Tests.*     → Tests.Common
```

Target framework: **net8.0**. `Nullable` and `ImplicitUsings` are `enable` in every project.

---

## 2. Domain layer (`Domain/`)

Rules:

1. One folder per aggregate / entity: `Domain/Customers/`, `Domain/PointTransactions/`, `Domain/Rewards/`.
2. Each entity has a strongly-typed ID as a `record` value object, e.g.

   ```csharp
   public record CustomerId(Guid Value)
   {
       public static CustomerId New() => new(Guid.NewGuid());
       public static CustomerId Empty() => new(Guid.Empty);
       public override string ToString() => Value.ToString();
   }
   ```
3. Entities have a **private constructor** + a `public static New(...)` factory. Mutating methods are explicit (`UpdateDetails`, `AddPoints`, `Redeem`, …) and protect invariants. Setters are `private`.
4. Enums live next to the entity that owns them (e.g. `TierLevel.cs`, `PointTransactionType.cs`).
5. **No** references to EF Core, MediatR, ASP.NET, or any other framework from `Domain`.
6. Business invariants enforced in domain methods (e.g. cannot redeem more points than `TotalPoints`, tier is recomputed from earned-points totals). Pure functions where possible so unit tests need no mocks.

For this task the domain entities are:

- `Customer` (`Id, Name, Email, Phone, TierLevel, TotalPoints, JoinDate`)
- `PointTransaction` (`Id, CustomerId, Points, Type, Description, CreatedAt`)
- `Reward` (`Id, Name, Description, PointsCost, Category, StockQuantity, IsActive`)

with enums `TierLevel { Bronze, Silver, Gold, Platinum }` and `PointTransactionType { Earned, Redeemed, Expired, Bonus }`.

---

## 3. Application layer (`Application/`)

Rules:

1. **Use cases are MediatR `IRequest<Result<TValue, TException>>`.** Each request lives in its own file together with its `IRequestHandler`. Validators are in a sibling file with the same prefix (e.g. `EarnPointsCommand.cs` + `EarnPointsCommandValidator.cs`).
2. Folder per feature: `Application/Customers/Commands/`, `Application/Customers/Queries/`, `Application/Customers/Exceptions/`, `Application/Rewards/...`, `Application/PointTransactions/...`.
3. **Result type** — copy the reference project's `Application/Common/Result.cs`. Handlers return `Result<TValue, TException>` and never throw for business errors. Pattern-match in controllers via `result.Match<ActionResult<...>>(success, e => e.ToObjectResult())`.
4. **Exceptions** — one abstract base per aggregate, plus concrete subclasses, all in `Application/<Feature>/Exceptions/`. Example pattern:

   ```csharp
   public abstract class CustomerException(CustomerId id, string message, Exception? inner = null)
       : Exception(message, inner) { public CustomerId CustomerId { get; } = id; }
   public class CustomerNotFoundException(CustomerId id) : CustomerException(id, $"Customer {id} not found");
   public class InsufficientPointsException(CustomerId id, int requested, int available)
       : CustomerException(id, $"Customer {id} has {available} points, cannot redeem {requested}");
   ```
5. **Repository / query interfaces** live under `Application/Common/Interfaces/Repositories` and `Application/Common/Interfaces/Queries`. Reads use `IXxxQueries`, writes use `IXxxRepository`. Reads return `Optional.Option<T>` (NuGet package `Optional`) instead of `null`.
6. **Validation** — FluentValidation. Each command has an `AbstractValidator<TCommand>`. The `ValidationBehaviour<TRequest, TResponse>` from `Application/Common/Behaviours/ValidationBehaviour.cs` is registered as an `IPipelineBehavior<,>` so validators run automatically on every request.
7. **Composition** — `Application/ConfigureApplication.cs` exposes `IServiceCollection.AddApplication()` which wires MediatR, validators, and the validation pipeline.

Use cases for this task:

| Endpoint | Command/Query |
|---|---|
| `GET    /api/customers/{id}` | `GetCustomerByIdQuery` |
| `POST   /api/customers` | `RegisterCustomerCommand` |
| `POST   /api/customers/{id}/earn` | `EarnPointsCommand` (applies Gold ×1.5, Platinum ×2 multiplier) |
| `POST   /api/customers/{id}/redeem` | `RedeemPointsCommand` (validates balance + reward stock) |
| `GET    /api/customers/{id}/history` | `GetPointTransactionHistoryQuery` |
| `GET    /api/rewards` | `GetAvailableRewardsQuery` |
| `POST   /api/rewards` | `CreateRewardCommand` |
| `GET    /api/customers/{id}/tier` | `GetCustomerTierQuery` (current tier + points to next) |

Tier thresholds are constants in the domain layer (`TierLevels.Bronze = 0, Silver = 1000, Gold = 5000, Platinum = 10000`) and derived from **total earned points** (sum of `Earned + Bonus` transactions, never decreased by redemptions).

Point expiration: a `PointTransaction` of type `Earned` or `Bonus` expires 12 months after `CreatedAt` if not yet consumed. Expiration is implemented as an explicit operation (`ExpirePointsCommand` or a query that subtracts from the available balance) so it is deterministic and unit-testable; callers pass `DateTime` to keep handlers pure.

---

## 4. Infrastructure layer (`Infrastructure/`)

Rules:

1. EF Core 8 + Npgsql + `EFCore.NamingConventions` (snake_case).
2. `Infrastructure/Persistence/ApplicationDbContext.cs` exposes `DbSet<Customer>`, `DbSet<PointTransaction>`, `DbSet<Reward>` and applies configurations from the assembly.
3. Each entity has an `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/`. Pattern (matches reference):
   - `builder.HasKey(x => x.Id);` with `HasConversion(x => x.Value, x => new XxxId(x))` for the strongly-typed ID.
   - Strings use `varchar(N)`. Decimals use `decimal(18, 2)`.
   - Enums stored as `varchar(50)` via `HasConversion(x => x.ToString(), x => Enum.Parse<...>(x))`.
   - `CreatedAt` defaults to `timezone('utc', now())`.
   - Foreign keys use `HasConstraintName("fk_<table>_<ref>_id")` + `OnDelete(DeleteBehavior.Cascade)`.
4. Repositories implement BOTH the write interface (`IXxxRepository`) and read interface (`IXxxQueries`). `ConfigurePersistence` registers them as `Scoped` and binds both interfaces to the same instance:

   ```csharp
   services.AddScoped<CustomerRepository>();
   services.AddScoped<ICustomerRepository>(sp => sp.GetRequiredService<CustomerRepository>());
   services.AddScoped<ICustomerQueries>(sp => sp.GetRequiredService<CustomerRepository>());
   ```
5. Read methods use `AsNoTracking()`. Pagination methods return `(IReadOnlyList<T>, int totalCount)`.
6. `ApplicationDbContextInitialiser` calls `Database.MigrateAsync()` on startup. Optional `SeedAsync` for default data (e.g. base rewards catalog).
7. Migrations live in `Infrastructure/Persistence/Migrations/`. Generate with `dotnet ef migrations add <Name> -p Infrastructure -s Api`.

---

## 5. API layer (`Api/`)

Rules:

1. `Program.cs` is the composition root. It calls `services.AddInfrastructure(Configuration)`, `services.AddApplication()`, `services.SetupServices()` (validators), configures auth/cors/swagger, then `await app.InitialiseDb()` + `app.MapControllers()`.
2. Controllers are thin. They:
   - Inject `ISender` (MediatR) and the relevant `IXxxQueries`.
   - Map a DTO → command, `await sender.Send(cmd)`, then `result.Match<ActionResult<...>>(dto, err => err.ToObjectResult())`.
3. Folder structure: `Api/Controllers/`, `Api/Dtos/`, `Api/Modules/Errors/`, `Api/Modules/Validators/` (DTO-level validators), `Api/Modules/Extensions/`, `Api/Modules/Attributes/`.
4. DTOs are `record`s with a `static FromDomainModel(T)` factory.
5. Each aggregate has an error handler in `Api/Modules/Errors/<Feature>ErrorHandler.cs` mapping exception subclasses to HTTP status codes via a `switch` expression. Mappings used in this project:
   - `*NotFoundException` → 404
   - `*AlreadyExistsException` → 409
   - `InsufficientPointsException`, `RewardOutOfStockException`, `RewardInactiveException` → 422 (Unprocessable Entity)
   - `*UnknownException` → 500
6. Routes use kebab-style only when the spec requires it. The task spec uses `/api/customers`, `/api/rewards` — set `[Route("api/customers")]`, `[Route("api/rewards")]`. Reference project used non-prefixed routes; we follow the spec here.
7. JSON: `System.Text.Json` defaults; enums serialized as strings (`JsonStringEnumConverter` registered in `Program.cs`).

> **Auth scope**: the task requirement does not mandate auth for the loyalty API. We keep the JWT scaffolding from the reference project optional — if implemented, only `POST /api/rewards` (admin) requires it. Simpler and acceptable: leave endpoints anonymous and document this in `docs/tasks/08-api-layer.md`.

---

## 6. Tests

### 6.1 Test infrastructure (`Tests.Common/`)

1. `IntegrationTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime` spins up a `Testcontainers.PostgreSql` container per test class fixture, replaces the registered `DbContextOptions<ApplicationDbContext>` with the container's connection string, and runs migrations. Copy `Tests.Common/TestFactory.cs` from the reference project verbatim, adjusting only the DB name.
2. `BaseIntegrationTest : IClassFixture<IntegrationTestWebFactory>` exposes `Context` (the `ApplicationDbContext`) and `Client` (an `HttpClient`). It supports a `TestAuthHandler` for header-driven test auth (only used if endpoints require auth). `SaveChangesAsync()` clears the EF change-tracker after each save so the context reads fresh state — keep this helper.
3. `Api/appsettings.test.json` mirrors `appsettings.json` but with neutral connection-string values (overridden by the container at runtime).

### 6.2 Test data (`Test.Data/`)

1. AutoFixture (or Bogus) is the data generator. The fields safe to randomize per the spec are:
   - `Customer`: `Name`, `Email`, `Phone`, `JoinDate`
   - `PointTransaction`: `Description`, `CreatedAt`
   - `Reward`: `Name`, `Description`, `Category`
   Business-rule fields (`TierLevel`, `TotalPoints`, `Points`, `Type`, `PointsCost`, `StockQuantity`, `IsActive`, `CustomerId`) are set explicitly per test.
2. Provide static helpers `CustomersData`, `PointTransactionsData`, `RewardsData` similar to the reference project's `UsersData`/`OrdersData`. Each helper accepts the business-rule values as parameters and randomizes the rest.
3. For perf and integration scenarios, expose a `LoyaltySeeder` that fills the DB with **≥ 10 000 records** distributed proportionally:
   - ~500 customers
   - ~50 rewards
   - ~9 450 point transactions (mix of `Earned`, `Redeemed`, `Bonus`, `Expired`) referencing existing customers, with realistic `CreatedAt` spread across 18 months.

### 6.3 Unit tests (`Api.Tests.Unit/`)

xUnit + FluentAssertions. No mocks for pure domain behaviour:

- `TierLevel.FromTotalEarnedPoints(int)` — boundary tests at 0, 999, 1000, 4999, 5000, 9999, 10000.
- `PointMultiplier.For(TierLevel)` — 1× / 1× / 1.5× / 2×.
- `Customer.Earn(int basePoints, DateTime at)` — applies multiplier, updates `TotalPoints`, raises tier when threshold crossed.
- `Customer.Redeem(Reward, int points, DateTime at)` — fails when balance < cost, when reward stock = 0, when reward inactive.
- `PointTransaction.IsExpired(DateTime now)` — true at exactly +12 months, false at +12 months − 1 ms, false for `Redeemed`/`Expired`.

Mock-based tests for handlers may use `NSubstitute` (or hand-rolled fakes — the reference project uses neither and unit tests are domain-only; we follow that).

### 6.4 Integration tests (`Api.Tests.Integrations/`)

xUnit + `WebApplicationFactory` + Testcontainers. One folder per controller:

- `Customers/CustomersControllerTests.cs` — register, get, full earn → tier-up flow, redeem, history.
- `Rewards/RewardsControllerTests.cs` — list (only active + in-stock), create.

Each test class implements `IAsyncLifetime` to seed/clean its own data, exactly like `UsersControllerTests` in the reference project.

### 6.5 Database tests (Testcontainers)

These live next to integration tests but assert at the **DbContext** level (no HTTP):

- Point-balance consistency: after N concurrent earn/redeem operations, sum of `Earned + Bonus` − `Redeemed − Expired` for the customer matches `TotalPoints` materialized on `Customer` (or the computed projection — pick one, document the choice in the test).
- Transaction-history ordering and FK integrity.
- Reward stock decrement under concurrent redemptions (use serializable isolation or a row-level lock — verify behaviour explicitly).

### 6.6 Performance tests (`perf/`)

k6 scripts:

- `perf/balance-load.js` — load test on `GET /api/customers/{id}` (point balance lookup). Target: 200 RPS for 2 min, p95 < 200 ms.
- `perf/redeem-stress.js` — stress test on `POST /api/customers/{id}/redeem` with concurrent VUs all targeting the same scarce reward (StockQuantity = 50). Verify no over-redeem and no negative balance.

Tests run against a live API instance pre-seeded with the 10 000-record dataset described in 6.2.

---

## 7. Conventions

- **Naming**: PascalCase for everything public; folders named after aggregates (plural form).
- **No `var`-vs-explicit policy** — match the reference project (mostly `var`, explicit when type-disambiguation matters).
- **Async**: every I/O method is `async` and takes a `CancellationToken`. Suffix is `Async` only on infrastructure methods if the reference project uses it; the reference project mostly omits the suffix in command handlers — we follow that.
- **One type per file** for public types. Validator may live alongside the command in the same folder.
- **Comments**: only when intent isn't obvious from code. No restating of what code does.
- **Commits/PRs**: one task = one commit = one PR. See `docs/` for the ordered task list.

---

## 8. Tooling versions

| Concern | Version |
|---|---|
| .NET SDK | 8.0 |
| EF Core / Npgsql.EFCore.PostgreSQL | 8.x / 9.x |
| EFCore.NamingConventions | 9.x |
| MediatR | 12.4.x |
| FluentValidation | 11.x |
| Optional | 4.x |
| xUnit / FluentAssertions | 2.4.x / 7.x |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.x |
| Testcontainers.PostgreSql | 4.x |
| AutoFixture (and AutoFixture.Xunit2) | 4.18.x |
| Bogus (optional, alternative to AutoFixture) | 35.x |
| k6 | 0.50+ (run via `grafana/k6` Docker image in CI) |

Pin versions in `.csproj` files (no floating ranges).

---

## 9. CI (GitHub Actions)

`.github/workflows/ci.yml` runs on `push` and `pull_request`:

1. `actions/setup-dotnet@v4` with `dotnet-version: 8.x`.
2. `dotnet restore` → `dotnet build -c Release --no-restore`.
3. `dotnet test -c Release --no-build --logger "trx" --collect:"XPlat Code Coverage"` for the `Api.Tests.Unit` and `Api.Tests.Integrations` projects. Testcontainers requires Docker — the GitHub-hosted `ubuntu-latest` runner provides it out of the box.
4. (Optional) k6 job that runs the perf scripts against a `docker compose` service stack on `workflow_dispatch` only — not on every PR.
5. Upload `*.trx` test results and coverage reports as artifacts.

The CI pipeline must be **green on every PR** before merging.

---

## 10. How to add a new feature (step-by-step recipe)

When adding a new endpoint / use case, do them in this order:

1. **Domain** — add/extend entity + value-object IDs + enums; add invariant-protecting methods + unit tests.
2. **Application** — add `XxxCommand`/`XxxQuery` + handler + validator + exceptions + repository/query interface methods.
3. **Infrastructure** — extend repository/query implementations; add EF configuration if a new entity; create migration; register in DI.
4. **API** — add DTO(s), DTO validator, controller action, error-handler mapping if a new exception type was introduced.
5. **Tests** — unit tests for any new domain logic; integration test for the new endpoint; perf script if the endpoint is on a hot path.
6. **Docs** — update `docs/tasks/<NN>-<name>.md` with what changed and why; one PR per task.

Always run `dotnet build` and `dotnet test` locally before opening a PR.
