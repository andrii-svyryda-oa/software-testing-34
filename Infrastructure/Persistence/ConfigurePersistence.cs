using Application.Common.Interfaces;
using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure.Persistence;

public static class ConfigurePersistence
{
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
}
