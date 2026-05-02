# Task 01 — Solution scaffold (clean architecture skeleton)

## Goal

Create the empty Clean Architecture skeleton: 4 production projects + 3 test projects, all wired in `SoftwareTesting.sln` with the correct project references and no business code yet. After this task the solution must `dotnet build` cleanly.

## Why this is a separate task

Establishes the project boundaries up front, so subsequent tasks can only add code in the right place — no accidental cross-layer leaks.

## Prerequisites

- .NET SDK 8.0 installed.
- Existing `SoftwareTesting.sln` and stub `Api/` from current state. The current `Api` project (with `WeatherForecastController.cs` and `WeatherForecast.cs`) is replaced by a clean Web API project pinned to net8.0.

## Files to add / edit

### Delete (cleanup of stub code)

- `Api/Controllers/WeatherForecastController.cs`
- `Api/WeatherForecast.cs`
- `Api/Api.http` *(optional — keep if you want quick local hits)*

### Create projects

```
Domain/Domain.csproj                   # classlib net8.0, Nullable+ImplicitUsings enable
Application/Application.csproj         # classlib net8.0, refs Domain
Infrastructure/Infrastructure.csproj   # classlib net8.0, refs Application + Domain
Tests.Common/Tests.Common.csproj       # classlib net8.0, refs Api + Application + Infrastructure + Test.Data
Test.Data/Test.Data.csproj             # classlib net8.0, refs Domain
Api.Tests.Unit/Api.Tests.Unit.csproj   # xUnit test project, refs Tests.Common
Api.Tests.Integrations/Api.Tests.Integrations.csproj  # xUnit test project, refs Api+Application+Infrastructure+Test.Data
```

Use the reference project's `.csproj` files as templates (`Z:\UNIK\ASP_NET_REST_API_PROJECT\<Project>\<Project>.csproj`) — copy structure verbatim, then strip out package references that are introduced by later tasks. For task 01, packages are minimal:

| Project | Package references |
|---|---|
| `Domain` | (none) |
| `Application` | (none yet — added in task 03) |
| `Infrastructure` | (none yet — added in task 07) |
| `Api` | `Swashbuckle.AspNetCore` (already present), `Microsoft.AspNetCore.OpenApi` if needed |
| `Tests.Common` | `Microsoft.AspNetCore.Mvc.Testing` 8.0.x, `Microsoft.NET.Test.Sdk` 17.6.x, `xunit` 2.4.x, `xunit.runner.visualstudio`, `Testcontainers.PostgreSql` 4.0.x, `Microsoft.Extensions.DependencyInjection` 9.0.x, `coverlet.collector` |
| `Test.Data` | (none yet — added in task 09) |
| `Api.Tests.Unit` | `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `FluentAssertions` 7.x, `coverlet.collector` |
| `Api.Tests.Integrations` | same as `Api.Tests.Unit` plus reference to `Tests.Common` |

> Note: `Tests.Common` carries the `Mvc.Testing` and `Testcontainers` packages so concrete test projects only need to reference `Tests.Common`.

### Wire into `SoftwareTesting.sln`

Add all seven projects to the solution. Use a `tests` solution folder for the three test projects (matches reference). Run:

```bash
dotnet sln add Domain/Domain.csproj
dotnet sln add Application/Application.csproj
dotnet sln add Infrastructure/Infrastructure.csproj
dotnet sln add Test.Data/Test.Data.csproj
dotnet sln add Tests.Common/Tests.Common.csproj
dotnet sln add Api.Tests.Unit/Api.Tests.Unit.csproj --solution-folder tests
dotnet sln add Api.Tests.Integrations/Api.Tests.Integrations.csproj --solution-folder tests
```

### Edit `Api/Api.csproj`

Add project references to `Application` and `Infrastructure`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Application\Application.csproj" />
  <ProjectReference Include="..\Infrastructure\Infrastructure.csproj" />
</ItemGroup>
```

Set `<PreserveCompilationContext>true</PreserveCompilationContext>` (needed by `WebApplicationFactory` later).

### Edit `Api/Program.cs`

Strip the WeatherForecast example. End state for this task:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.Run();

public partial class Program;
```

The `public partial class Program;` line is required so `WebApplicationFactory<Program>` works in tests.

### Add a placeholder type to each empty class library

To stop compilers / tooling from complaining about empty assemblies, add one stub file per library (deleted in the next task). Example: `Domain/_Placeholder.cs` with `namespace Domain; internal static class _Placeholder;`.

## Acceptance criteria

- `dotnet build SoftwareTesting.sln -c Release` succeeds with **0 errors and 0 warnings**.
- `dotnet test SoftwareTesting.sln` runs (zero tests is acceptable at this stage; the command must exit 0).
- `dotnet run --project Api` starts on the default Kestrel port and serves Swagger at `/swagger`.
- Project graph matches §1 of `AGENTS.md`. Verify with `dotnet list <Project>/<Project>.csproj reference` for each project.

## Out of scope (handled in later tasks)

- Domain entities (task 02)
- MediatR / FluentValidation / Result type (task 03)
- DbContext / migrations (task 07)
- Controllers / DTOs (task 08)
- Any tests (tasks 10–13)
- CI workflow (task 14)

## Commit message

```
Task 01: solution scaffold (clean architecture skeleton)

Adds Domain/Application/Infrastructure/Test.Data/Tests.Common/
Api.Tests.Unit/Api.Tests.Integrations projects, wires them into
SoftwareTesting.sln with correct one-way references, and removes the
WeatherForecast template from Api.
```
