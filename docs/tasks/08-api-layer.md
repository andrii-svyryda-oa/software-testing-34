# Task 08 — API: controllers, DTOs, error handlers, Program.cs

## Goal

Expose the eight endpoints listed in `task_requirement.txt` via thin ASP.NET controllers, with DTOs, DTO-level FluentValidation, error handlers mapping exceptions to HTTP status codes, and a fully-wired `Program.cs`.

## Endpoint table (matches the spec)

| Method | Route | Handler / Query |
|---|---|---|
| `GET`  | `/api/customers/{id}`         | `GetCustomerByIdQuery`         |
| `POST` | `/api/customers`              | `RegisterCustomerCommand`      |
| `POST` | `/api/customers/{id}/earn`    | `EarnPointsCommand`            |
| `POST` | `/api/customers/{id}/redeem`  | `RedeemPointsCommand`          |
| `GET`  | `/api/customers/{id}/history` | `GetCustomerHistoryQuery`      |
| `GET`  | `/api/customers/{id}/tier`    | `GetCustomerTierQuery`         |
| `GET`  | `/api/rewards`                | `GetAvailableRewardsQuery`     |
| `POST` | `/api/rewards`                | `CreateRewardCommand`          |

## Files to add

```
Api/Api.csproj                                               # add NuGet packages
Api/Program.cs                                               # finalize composition root
Api/appsettings.json                                         # ensure ConnectionStrings:Default
Api/appsettings.Development.json
Api/Controllers/CustomersController.cs
Api/Controllers/RewardsController.cs
Api/Dtos/CustomerDtos.cs
Api/Dtos/PointTransactionDtos.cs
Api/Dtos/RewardDtos.cs
Api/Dtos/PaginatedData.cs
Api/Modules/SetupModule.cs                                   # AddValidators
Api/Modules/DbModule.cs                                      # InitialiseDb
Api/Modules/Errors/CustomerErrorHandler.cs
Api/Modules/Errors/RewardErrorHandler.cs
Api/Modules/Errors/PointTransactionErrorHandler.cs
Api/Modules/Errors/ValidationProblemDetailsMiddleware.cs    # maps FluentValidation exceptions
Api/Modules/Validators/CustomerDtoValidators.cs
Api/Modules/Validators/RewardDtoValidators.cs
```

## NuGet packages — add to `Api.csproj`

```xml
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
<PackageReference Include="MediatR" Version="12.4.1" />  <!-- transitive via Application but explicit here helps clarity -->
```

(Already present from earlier tasks: `Swashbuckle.AspNetCore`, `Microsoft.EntityFrameworkCore.Design`.)

## DTOs

### `CustomerDtos.cs`

```csharp
public record CustomerDto(
    Guid Id, string Name, string Email, string Phone,
    TierLevel TierLevel, int TotalPoints, int TotalEarnedPoints, DateTime JoinDate)
{
    public static CustomerDto FromDomainModel(Customer c) => new(
        c.Id.Value, c.Name, c.Email, c.Phone,
        c.TierLevel, c.TotalPoints, c.TotalEarnedPoints, c.JoinDate);
}

public record RegisterCustomerDto(string Name, string Email, string Phone);

public record EarnPointsDto(int BasePoints, string? Description);

public record RedeemPointsDto(Guid RewardId);

public record CustomerTierDto(
    TierLevel Current, TierLevel Next, int TotalEarnedPoints, int PointsToNext)
{
    public static CustomerTierDto FromResult(CustomerTierResult r) =>
        new(r.Current, r.Next, r.TotalEarnedPoints, r.PointsToNext);
}
```

### `PointTransactionDtos.cs`

```csharp
public record PointTransactionDto(
    Guid Id, Guid CustomerId, int Points, PointTransactionType Type,
    string Description, DateTime CreatedAt)
{
    public static PointTransactionDto FromDomainModel(PointTransaction t) =>
        new(t.Id.Value, t.CustomerId.Value, t.Points, t.Type, t.Description, t.CreatedAt);
}
```

### `RewardDtos.cs`

```csharp
public record RewardDto(
    Guid Id, string Name, string Description, int PointsCost,
    string Category, int StockQuantity, bool IsActive)
{
    public static RewardDto FromDomainModel(Reward r) => new(
        r.Id.Value, r.Name, r.Description, r.PointsCost,
        r.Category, r.StockQuantity, r.IsActive);
}

public record CreateRewardDto(
    string Name, string Description, int PointsCost,
    string Category, int StockQuantity);
```

### `PaginatedData.cs`

```csharp
public record PaginatedData<T>(IReadOnlyList<T> Data, int Total);
```

## Controllers

### `CustomersController`

```csharp
[ApiController]
[Route("api/customers")]
public class CustomersController(ISender sender, ICustomerQueries customerQueries) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken ct)
    {
        var customer = await sender.Send(new GetCustomerByIdQuery(id), ct);
        return customer.Match<ActionResult<CustomerDto>>(
            c => CustomerDto.FromDomainModel(c),
            () => NotFound());
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Register([FromBody] RegisterCustomerDto dto, CancellationToken ct)
    {
        var result = await sender.Send(new RegisterCustomerCommand
        {
            Name = dto.Name, Email = dto.Email, Phone = dto.Phone
        }, ct);
        return result.Match<ActionResult<CustomerDto>>(
            c => CreatedAtAction(nameof(Get), new { id = c.Id.Value }, CustomerDto.FromDomainModel(c)),
            e => e.ToObjectResult());
    }

    [HttpPost("{id:guid}/earn")]
    public async Task<ActionResult<CustomerDto>> Earn(Guid id, [FromBody] EarnPointsDto dto, CancellationToken ct)
    {
        var result = await sender.Send(new EarnPointsCommand
        {
            CustomerId = id,
            BasePoints = dto.BasePoints,
            Description = dto.Description ?? "Points earned"
        }, ct);
        return result.Match<ActionResult<CustomerDto>>(
            c => CustomerDto.FromDomainModel(c),
            e => e.ToObjectResult());
    }

    [HttpPost("{id:guid}/redeem")]
    public async Task<ActionResult<CustomerDto>> Redeem(Guid id, [FromBody] RedeemPointsDto dto, CancellationToken ct)
    {
        var result = await sender.Send(new RedeemPointsCommand
        {
            CustomerId = id, RewardId = dto.RewardId
        }, ct);
        return result.Match<ActionResult<CustomerDto>>(
            c => CustomerDto.FromDomainModel(c),
            e => e.ToObjectResult());
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<PaginatedData<PointTransactionDto>>> History(
        Guid id, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var take = Math.Clamp(pageSize, 1, 100);
        var result = await sender.Send(new GetCustomerHistoryQuery(id, skip, take), ct);
        return result.Match<ActionResult<PaginatedData<PointTransactionDto>>>(
            page => new PaginatedData<PointTransactionDto>(
                page.Data.Select(PointTransactionDto.FromDomainModel).ToList(),
                page.Total),
            e => e.ToObjectResult());
    }

    [HttpGet("{id:guid}/tier")]
    public async Task<ActionResult<CustomerTierDto>> Tier(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCustomerTierQuery(id), ct);
        return result.Match<ActionResult<CustomerTierDto>>(
            t => CustomerTierDto.FromResult(t),
            e => e.ToObjectResult());
    }
}
```

### `RewardsController`

```csharp
[ApiController]
[Route("api/rewards")]
public class RewardsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RewardDto>>> List(CancellationToken ct)
    {
        var rewards = await sender.Send(new GetAvailableRewardsQuery(), ct);
        return Ok(rewards.Select(RewardDto.FromDomainModel).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<RewardDto>> Create([FromBody] CreateRewardDto dto, CancellationToken ct)
    {
        var result = await sender.Send(new CreateRewardCommand
        {
            Name = dto.Name, Description = dto.Description,
            PointsCost = dto.PointsCost, Category = dto.Category,
            StockQuantity = dto.StockQuantity
        }, ct);
        return result.Match<ActionResult<RewardDto>>(
            r => CreatedAtAction(nameof(List), null, RewardDto.FromDomainModel(r)),
            e => e.ToObjectResult());
    }
}
```

## Error handlers

### `CustomerErrorHandler`

```csharp
public static class CustomerErrorHandler
{
    public static ObjectResult ToObjectResult(this CustomerException ex) => new(ex.Message)
    {
        StatusCode = ex switch
        {
            CustomerNotFoundException                => StatusCodes.Status404NotFound,
            CustomerAlreadyExistsException           => StatusCodes.Status409Conflict,
            InsufficientPointsException
                or RedeemRewardOutOfStockException
                or RedeemRewardInactiveException     => StatusCodes.Status422UnprocessableEntity,
            RedeemRewardNotFoundException            => StatusCodes.Status404NotFound,
            CustomerUnknownException                 => StatusCodes.Status500InternalServerError,
            _ => throw new NotImplementedException($"Unmapped customer exception: {ex.GetType().Name}")
        }
    };
}
```

### `RewardErrorHandler`

```csharp
public static class RewardErrorHandler
{
    public static ObjectResult ToObjectResult(this RewardException ex) => new(ex.Message)
    {
        StatusCode = ex switch
        {
            RewardNotFoundException     => StatusCodes.Status404NotFound,
            RewardOutOfStockException
                or RewardInactiveException => StatusCodes.Status422UnprocessableEntity,
            RewardUnknownException      => StatusCodes.Status500InternalServerError,
            _ => throw new NotImplementedException($"Unmapped reward exception: {ex.GetType().Name}")
        }
    };
}
```

### Validation problem details middleware

`ValidationBehaviour` throws `FluentValidation.ValidationException`. Wire a small middleware that catches it and returns RFC 7807 ProblemDetails with HTTP 400:

```csharp
public class ValidationExceptionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (ValidationException vex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/problem+json";
            var details = new ValidationProblemDetails(
                vex.Errors.GroupBy(e => e.PropertyName)
                          .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            { Status = 400, Title = "Validation failed" };
            await ctx.Response.WriteAsJsonAsync(details);
        }
    }
}
```

Register in `Program.cs` before `app.MapControllers()`.

## DTO validators

DTO-level validators (registered automatically by `AddFluentValidationAutoValidation()`) provide the *first line* of defence so we return 400 before the request even reaches MediatR. Mirrors the reference's `Api/Modules/Validators/UserDtoValidator.cs`. Same rules as the command validators but applied to DTOs.

## Program.cs (final shape)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.SetupServices();   // FluentValidation auto-validation

var app = builder.Build();

app.UseMiddleware<ValidationExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await app.InitialiseDb();
app.MapControllers();
app.Run();

public partial class Program;
```

## Acceptance criteria

- All 8 endpoints respond with the documented status codes (verified manually via Swagger or `curl`).
- Validation failures return 400 ProblemDetails with field-level errors.
- Business errors return 404 / 409 / 422 / 500 per the error-handler tables.
- Successful POSTs that create resources return 201 Created with a `Location` header (where applicable).
- Enums in JSON are serialized as strings (`"Bronze"`, `"Earned"` …).
- The full chain works end-to-end: register → earn 6× of 1000 base points → tier becomes `Gold` and 6th call yields `awarded = 1500` (visible via `/api/customers/{id}` and `/api/customers/{id}/tier`).

## Verification

Run `dotnet run --project Api` against a local Postgres and exercise via Swagger:

```bash
# 1. Register
curl -X POST http://localhost:5000/api/customers \
     -H 'content-type: application/json' \
     -d '{"name":"Alice","email":"a@x.io","phone":"+380501112233"}'
# 2. Earn 1500 points (Bronze, multiplier 1×)
curl -X POST http://localhost:5000/api/customers/{id}/earn -d '{"basePoints":1500}'
# 3. /tier → Current=Silver, Next=Gold, PointsToNext=3500
```

## Out of scope

- Auth (the spec does not require it).
- Tests (tasks 11/12).

## Commit message

```
Task 08: API layer (controllers, DTOs, error handlers, Program.cs)

Wires all eight loyalty endpoints with thin controllers + MediatR,
DTO-level FluentValidation, RFC 7807 problem-details middleware for
validation errors, and per-aggregate error handlers mapping
domain/application exceptions to HTTP status codes.
```
