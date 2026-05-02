# Task 03 — Application layer common building blocks

## Goal

Add the layer-wide infrastructure pieces that all use cases will consume: `Result<T,E>`, MediatR registration, FluentValidation pipeline behaviour, repository / query interfaces, and exception bases. After this task, no use cases exist yet — but the soil is ready for them.

## Files to add

```
Application/Application.csproj                              # add NuGet refs (see below)
Application/ConfigureApplication.cs                         # AddApplication() extension
Application/Common/Result.cs                                # copy from reference verbatim
Application/Common/Behaviours/ValidationBehaviour.cs        # copy from reference verbatim
Application/Common/Interfaces/Repositories/ICustomerRepository.cs
Application/Common/Interfaces/Repositories/IRewardRepository.cs
Application/Common/Interfaces/Repositories/IPointTransactionRepository.cs
Application/Common/Interfaces/Queries/ICustomerQueries.cs
Application/Common/Interfaces/Queries/IRewardQueries.cs
Application/Common/Interfaces/Queries/IPointTransactionQueries.cs
Application/Customers/Exceptions/CustomerException.cs
Application/Rewards/Exceptions/RewardException.cs
Application/PointTransactions/Exceptions/PointTransactionException.cs
```

## NuGet packages — add to `Application.csproj`

```xml
<ItemGroup>
  <PackageReference Include="MediatR" Version="12.4.1" />
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
  <PackageReference Include="Optional" Version="4.0.0" />
</ItemGroup>
```

## Result<TValue, TError>

Copy `Application/Common/Result.cs` from `Z:\UNIK\ASP_NET_REST_API_PROJECT\Application\Common\Result.cs` byte-for-byte. Do not change semantics — controllers in task 08 rely on `.Match`, `.MatchAsync`, `.Bind`, `.BindAsync`, `.Map`.

## ValidationBehaviour

Copy `Application/Common/Behaviours/ValidationBehaviour.cs` from the reference verbatim. It throws `FluentValidation.ValidationException` on failures; the API layer (task 08) wires this into a problem-details middleware.

## ConfigureApplication.cs

```csharp
public static class ConfigureApplication
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    }
}
```

> No `IPasswordHasher` / `IJwtService` registrations — auth is out of scope for this task spec.

## Repository interfaces (writes)

### `ICustomerRepository`

```csharp
Task<Customer> Add(Customer customer, CancellationToken ct);
Task<Customer> Update(Customer customer, CancellationToken ct);
Task<Option<Customer>> GetById(CustomerId id, CancellationToken ct);
Task<Option<Customer>> GetByEmail(string email, CancellationToken ct);
```

### `IRewardRepository`

```csharp
Task<Reward> Add(Reward reward, CancellationToken ct);
Task<Reward> Update(Reward reward, CancellationToken ct);
Task<Option<Reward>> GetById(RewardId id, CancellationToken ct);
```

### `IPointTransactionRepository`

```csharp
Task<PointTransaction> Add(PointTransaction transaction, CancellationToken ct);
Task<List<PointTransaction>> AddMany(List<PointTransaction> txs, CancellationToken ct);
Task<List<PointTransaction>> GetActiveEarnedFor(CustomerId customerId, CancellationToken ct);
    // returns Earned+Bonus transactions whose IsExpired(DateTime.UtcNow) == false, oldest first.
```

## Query interfaces (reads, AsNoTracking)

### `ICustomerQueries`

```csharp
Task<Option<Customer>> GetById(CustomerId id, CancellationToken ct);
Task<Option<Customer>> GetByEmail(string email, CancellationToken ct);
```

### `IRewardQueries`

```csharp
Task<IReadOnlyList<Reward>> GetAvailable(CancellationToken ct);
    // IsActive == true && StockQuantity > 0
Task<Option<Reward>> GetById(RewardId id, CancellationToken ct);
```

### `IPointTransactionQueries`

```csharp
Task<IReadOnlyList<PointTransaction>> GetHistoryFor(CustomerId customerId, int skip, int take, CancellationToken ct);
Task<int> CountFor(CustomerId customerId, CancellationToken ct);
```

## Exception base classes

Mirror the reference project's `UserException` shape. Each aggregate gets one file with the abstract base + concrete subclasses.

### `CustomerException.cs`

```csharp
public abstract class CustomerException(CustomerId id, string message, Exception? inner = null)
    : Exception(message, inner) { public CustomerId CustomerId { get; } = id; }

public class CustomerNotFoundException(CustomerId id)
    : CustomerException(id, $"Customer {id} not found");
public class CustomerAlreadyExistsException(CustomerId id)
    : CustomerException(id, $"Customer with this email already exists ({id})");
public class InsufficientPointsException(CustomerId id, int requested, int available)
    : CustomerException(id, $"Customer {id} has only {available} redeemable points; requested {requested}");
public class CustomerUnknownException(CustomerId id, Exception inner)
    : CustomerException(id, $"Unknown error for customer {id}", inner);
```

### `RewardException.cs`

```csharp
public abstract class RewardException(RewardId id, string message, Exception? inner = null)
    : Exception(message, inner) { public RewardId RewardId { get; } = id; }

public class RewardNotFoundException(RewardId id)         : RewardException(id, $"Reward {id} not found");
public class RewardOutOfStockException(RewardId id)       : RewardException(id, $"Reward {id} is out of stock");
public class RewardInactiveException(RewardId id)         : RewardException(id, $"Reward {id} is inactive");
public class RewardUnknownException(RewardId id, Exception inner)
    : RewardException(id, $"Unknown error for reward {id}", inner);
```

### `PointTransactionException.cs`

```csharp
public abstract class PointTransactionException(PointTransactionId id, string message, Exception? inner = null)
    : Exception(message, inner) { public PointTransactionId TransactionId { get; } = id; }

public class PointTransactionUnknownException(PointTransactionId id, Exception inner)
    : PointTransactionException(id, $"Unknown error for point transaction {id}", inner);
```

> `RedeemPointsCommand` (task 04) returns `Result<Customer, CustomerException>` and uses both `InsufficientPointsException` (a `CustomerException`) and a thin reward-related shim. To keep the result type single-typed, define a dedicated `RedeemPointsException` base in task 04, or wrap reward exceptions inside customer exceptions. The simplest path used in the reference project is per-handler: pick one error union, document it. We'll use `CustomerException` as the union for the customer flow and pass reward issues as `InsufficientPointsException` style subclasses (`RedeemRewardOutOfStockException`, `RedeemRewardInactiveException`) under `CustomerException`. Add those subclasses in this file.

Final additions to `CustomerException.cs`:

```csharp
public class RedeemRewardOutOfStockException(CustomerId id, RewardId rewardId)
    : CustomerException(id, $"Reward {rewardId} is out of stock");
public class RedeemRewardInactiveException(CustomerId id, RewardId rewardId)
    : CustomerException(id, $"Reward {rewardId} is inactive");
public class RedeemRewardNotFoundException(CustomerId id, RewardId rewardId)
    : CustomerException(id, $"Reward {rewardId} not found");
```

## Acceptance criteria

- `dotnet build Application` succeeds, 0 warnings.
- `Application` references only `Domain` (and the listed NuGet packages). No reference to `Infrastructure`, `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`.
- `AddApplication()` is callable from `Api/Program.cs` (you can add the call in this PR, but it's a no-op until task 04).

## Verification

- Open the solution; the `Application` project's dependency graph in IDE shows only `Domain`.
- `grep -r "EntityFrameworkCore" Application/` returns nothing.
- All exception classes inherit `Exception` directly via the abstract base; none of them inherit `ApplicationException` or other framework types.

## Out of scope

- Concrete commands / queries / handlers (tasks 04–06).
- DI binding for the auth/security pieces (`IPasswordHasher`, `IJwtService`) — auth not required by spec.

## Commit message

```
Task 03: application-layer common building blocks

Adds Result<T,E>, ValidationBehaviour, MediatR/FluentValidation
registration, repository/query interfaces, and exception bases for
Customer/Reward/PointTransaction. No use cases yet.
```
