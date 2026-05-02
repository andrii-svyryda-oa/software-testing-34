# Task 07 — Infrastructure: DbContext, configurations, repositories, migrations

## Goal

Implement the EF Core 8 + PostgreSQL persistence layer: `ApplicationDbContext`, entity configurations with strongly-typed-id conversions, snake_case naming, repositories implementing both write and read interfaces, the initialiser, and the initial migration.

## Files to add

```
Infrastructure/Infrastructure.csproj                                # add NuGet packages
Infrastructure/ConfigureInfrastructure.cs
Infrastructure/Persistence/ApplicationDbContext.cs
Infrastructure/Persistence/ApplicationDbContextInitialiser.cs
Infrastructure/Persistence/ConfigurePersistence.cs
Infrastructure/Persistence/Configurations/CustomerConfiguration.cs
Infrastructure/Persistence/Configurations/PointTransactionConfiguration.cs
Infrastructure/Persistence/Configurations/RewardConfiguration.cs
Infrastructure/Persistence/Repositories/CustomerRepository.cs
Infrastructure/Persistence/Repositories/PointTransactionRepository.cs
Infrastructure/Persistence/Repositories/RewardRepository.cs
Infrastructure/Persistence/Migrations/<timestamp>_InitialCreate.cs   # generated
Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs  # generated
```

## NuGet packages — add to `Infrastructure.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.1" />
  <PackageReference Include="EFCore.NamingConventions" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
</ItemGroup>
```

Also add to `Api/Api.csproj` (design-time only):

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.11">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

## ApplicationDbContext

```csharp
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Customer>          Customers         { get; set; }
    public DbSet<PointTransaction>  PointTransactions { get; set; }
    public DbSet<Reward>            Rewards           { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}
```

No seeders here for the loyalty domain — seed test data via `Test.Data` in task 09.

## EF configurations (key rules per `AGENTS.md` §4)

### `CustomerConfiguration`

```csharp
builder.HasKey(x => x.Id);
builder.Property(x => x.Id).HasConversion(x => x.Value, x => new CustomerId(x));
builder.Property(x => x.Name).IsRequired().HasColumnType("varchar(255)");
builder.Property(x => x.Email).IsRequired().HasColumnType("varchar(255)");
builder.HasIndex(x => x.Email).IsUnique();           // enforces business uniqueness
builder.Property(x => x.Phone).IsRequired().HasColumnType("varchar(20)");
builder.Property(x => x.JoinDate).HasDefaultValueSql("timezone('utc', now())");
builder.Property(x => x.TotalPoints).HasColumnType("integer").HasDefaultValue(0);
builder.Property(x => x.TotalEarnedPoints).HasColumnType("integer").HasDefaultValue(0);
builder.Property(x => x.TierLevel)
    .IsRequired()
    .HasConversion(x => x.ToString(), x => Enum.Parse<TierLevel>(x))
    .HasColumnType("varchar(50)");
```

### `PointTransactionConfiguration`

```csharp
builder.HasKey(x => x.Id);
builder.Property(x => x.Id).HasConversion(x => x.Value, x => new PointTransactionId(x));
builder.Property(x => x.CustomerId).HasConversion(x => x.Value, x => new CustomerId(x));
builder.Property(x => x.Points).HasColumnType("integer").IsRequired();
builder.Property(x => x.Remaining).HasColumnType("integer").HasDefaultValue(0);
builder.Property(x => x.Description).IsRequired().HasColumnType("varchar(500)");
builder.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");
builder.Property(x => x.Type)
    .IsRequired()
    .HasConversion(x => x.ToString(), x => Enum.Parse<PointTransactionType>(x))
    .HasColumnType("varchar(50)");

builder.HasOne<Customer>()
    .WithMany()
    .HasForeignKey(x => x.CustomerId)
    .HasConstraintName("fk_point_transactions_customers_id")
    .OnDelete(DeleteBehavior.Cascade);

builder.HasIndex(x => new { x.CustomerId, x.CreatedAt });   // history pagination
builder.HasIndex(x => new { x.CustomerId, x.Type, x.CreatedAt }); // expiration scan
```

### `RewardConfiguration`

```csharp
builder.HasKey(x => x.Id);
builder.Property(x => x.Id).HasConversion(x => x.Value, x => new RewardId(x));
builder.Property(x => x.Name).IsRequired().HasColumnType("varchar(255)");
builder.Property(x => x.Description).IsRequired().HasColumnType("varchar(1000)");
builder.Property(x => x.Category).IsRequired().HasColumnType("varchar(50)");
builder.Property(x => x.PointsCost).HasColumnType("integer").IsRequired();
builder.Property(x => x.StockQuantity).HasColumnType("integer").IsRequired();
builder.Property(x => x.IsActive).HasColumnType("boolean").HasDefaultValue(true);

// Concurrency token — used by RedeemPointsCommand to detect overlapping redemptions.
builder.UseXminAsConcurrencyToken();
builder.HasIndex(x => x.Category);
builder.HasIndex(x => x.IsActive);
```

> `UseXminAsConcurrencyToken()` is a Npgsql extension that maps the PostgreSQL system column `xmin` to the entity. No domain change required.

## Repositories

Each repository implements both `IXxxRepository` and `IXxxQueries` (matches reference). Pattern from `UserRepository`:

```csharp
public class CustomerRepository(ApplicationDbContext context) : ICustomerRepository, ICustomerQueries
{
    public async Task<Option<Customer>> GetById(CustomerId id, CancellationToken ct)
    {
        var entity = await context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? Option.None<Customer>() : Option.Some(entity);
    }

    public async Task<Option<Customer>> GetByEmail(string email, CancellationToken ct) { /* ... */ }

    public async Task<Customer> Add(Customer customer, CancellationToken ct)
    {
        await context.Customers.AddAsync(customer, ct);
        await context.SaveChangesAsync(ct);
        return customer;
    }

    public async Task<Customer> Update(Customer customer, CancellationToken ct)
    {
        context.Customers.Update(customer);
        await context.SaveChangesAsync(ct);
        return customer;
    }
}
```

`PointTransactionRepository` implements `Add`, `AddMany`, `GetActiveEarnedFor`, `GetExpirableEarnedFor`, `GetHistoryFor` (newest first via `OrderByDescending(x => x.CreatedAt)`), `CountFor`.

`RewardRepository` implements `Add`, `Update`, `GetById`, `GetAvailable`. `GetAvailable` filters `IsActive == true && StockQuantity > 0` and orders by `Name`.

> **Important**: when handlers call multiple repository methods on the same `DbContext` within one request, each method's internal `SaveChangesAsync` causes multiple round-trips. To preserve atomicity for `RedeemPointsCommand` (customer + reward + transaction in one save), the redeem handler should write through the `DbContext` directly **once** at the end, OR introduce a thin `IUnitOfWork.SaveChangesAsync(ct)` and have the repositories' `Add`/`Update` only stage the entity. The reference project takes the simpler path (per-method save). Choose the simpler one here too, but in `RedeemPointsCommand`'s handler, manipulate `context` from a single repository call: implement `RedeemAtomically(Customer, Reward, PointTransaction, ct)` on `ICustomerRepository` (or a new `ILoyaltyTransactionRepository`) that updates all three in one `SaveChangesAsync`. Add this method here in task 07 and update the handler in task 04 in the same PR? No — to keep tasks independent, add a `SaveChangesAsync` on `ICustomerRepository` plus stage-only `AttachUpdate` / `AttachAdd` helpers. Document the chosen approach in the repository's XML doc.

Recommended concrete shape (added in this task; consumed by task 04 + task 06 once they exist):

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

Register in `ConfigurePersistence`:

```csharp
services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>().AsUnitOfWork());
```

…or simply add `IUnitOfWork` to `ApplicationDbContext` directly:

```csharp
public class ApplicationDbContext(...) : DbContext(...), IUnitOfWork { /* ... */ }
```

Then handlers use `customerRepository.Update(c, ct)` (which now stages without saving — adjust `Update` impls) and `unitOfWork.SaveChangesAsync(ct)` once at the end.

This is the **one** task where the choice between "per-method save" and "explicit unit-of-work" is finalized for the codebase. Default decision: **explicit `IUnitOfWork`**, because we need atomicity for the redeem flow. Update repository methods to stage only; revise tasks 04 and 06 if needed (or note that they were written assuming this; adjust the handler files in this PR if they exist already in branch).

## ConfigurePersistence

```csharp
public static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(configuration.GetConnectionString("Default"));
    var dataSource = dataSourceBuilder.Build();

    services.AddDbContext<ApplicationDbContext>(opts => opts
        .UseNpgsql(dataSource, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
        .UseSnakeCaseNamingConvention()
        .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));

    services.AddScoped<ApplicationDbContextInitialiser>();
    services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

    services.AddScoped<CustomerRepository>();
    services.AddScoped<ICustomerRepository>(sp => sp.GetRequiredService<CustomerRepository>());
    services.AddScoped<ICustomerQueries>(sp => sp.GetRequiredService<CustomerRepository>());

    services.AddScoped<RewardRepository>();
    services.AddScoped<IRewardRepository>(sp => sp.GetRequiredService<RewardRepository>());
    services.AddScoped<IRewardQueries>(sp => sp.GetRequiredService<RewardRepository>());

    services.AddScoped<PointTransactionRepository>();
    services.AddScoped<IPointTransactionRepository>(sp => sp.GetRequiredService<PointTransactionRepository>());
    services.AddScoped<IPointTransactionQueries>(sp => sp.GetRequiredService<PointTransactionRepository>());
}
```

## ApplicationDbContextInitialiser

```csharp
public class ApplicationDbContextInitialiser(ApplicationDbContext context)
{
    public Task InitializeAsync() => context.Database.MigrateAsync();
}
```

## Initial migration

```bash
dotnet ef migrations add InitialCreate -p Infrastructure -s Api -o Persistence/Migrations
```

The generated files go into `Infrastructure/Persistence/Migrations/`. Commit the generated `.cs` files (do not commit the generated SQL — there is none, EF runs migrations at startup).

## appsettings updates

Add to `Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=5432;Database=loyalty;User Id=postgres;Password=postgres;"
  }
}
```

Add `Api/appsettings.Development.json` (override for local dev, same shape).

## Acceptance criteria

- `dotnet build SoftwareTesting.sln` succeeds with 0 warnings.
- With a local Postgres available, `dotnet run --project Api` starts, `await app.InitialiseDb()` runs the migration, and Postgres now contains tables `customers`, `rewards`, `point_transactions` (snake_case).
- All FK / unique-index / concurrency-token configurations are present (verify by inspecting the generated migration `.cs`).
- The repositories are registered as scoped and resolvable for both interfaces.

## Verification

- `dotnet ef migrations script -p Infrastructure -s Api` produces SQL that creates the three tables, indexes, and FKs as configured.
- `psql -d loyalty -c '\d+ customers'` shows columns `id, name, email, phone, tier_level, total_points, total_earned_points, join_date`.

## Out of scope

- API wiring (task 08).
- Test seeders (task 09).
- Concurrency-retry policy in handlers (already discussed in task 04 — re-check it works against the migrated schema during task 12).

## Commit message

```
Task 07: persistence (EF Core + Postgres + snake_case)

Adds ApplicationDbContext, entity configurations with strongly-typed
ID conversions and concurrency token on Reward, repositories
implementing both write/read interfaces, IUnitOfWork on the context,
ConfigurePersistence DI extensions, the initialiser, and the
InitialCreate migration.
```
