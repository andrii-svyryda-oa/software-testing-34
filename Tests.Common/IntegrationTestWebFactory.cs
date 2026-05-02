using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Tests.Common;

public class IntegrationTestWebFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    static IntegrationTestWebFactory()
    {
        // Workaround for Testcontainers .NET on Windows: when the active Docker context endpoint
        // is stored as `npipe:////./pipe/docker_engine` (Docker Desktop default), Docker.DotNet's
        // URI parser drops the host and the static cctor of TestcontainersSettings explodes.
        // Setting DOCKER_HOST to the canonical 3-slash form before any Testcontainers type loads
        // bypasses the broken context discovery.
        if (OperatingSystem.IsWindows()
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "npipe://./pipe/docker_engine");
        }
    }

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("loyalty_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _db.GetConnectionString()
                });
            })
            .ConfigureTestServices(services =>
            {
                services.RemoveServiceByType(typeof(DbContextOptions<ApplicationDbContext>));

                var dataSource = new NpgsqlDataSourceBuilder(_db.GetConnectionString()).Build();
                services.AddDbContext<ApplicationDbContext>(o => o
                    .UseNpgsql(dataSource, b => b.MigrationsAssembly(
                        typeof(ApplicationDbContext).Assembly.FullName))
                    .UseSnakeCaseNamingConvention()
                    .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
    }

    public Task InitializeAsync() => _db.StartAsync();
    public new Task DisposeAsync() => _db.DisposeAsync().AsTask();
}

public static class TestFactoryExtensions
{
    public static void RemoveServiceByType(this IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(s => s.ServiceType == serviceType).ToList();
        foreach (var d in descriptors) services.Remove(d);
    }
}
