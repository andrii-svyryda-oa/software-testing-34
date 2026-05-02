using Application.Customers.Commands;
using Domain.Customers;
using Domain.PointTransactions;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Data.Builders;
using Tests.Common;
using Xunit;

namespace Api.Tests.Integrations.Database;

[Collection("IntegrationTests")]
public class PointBalanceConsistencyTests : BaseIntegrationTest, IAsyncLifetime
{
    public PointBalanceConsistencyTests(IntegrationTestWebFactory factory) : base(factory) { }

    public async Task InitializeAsync() => await ClearAllAsync();
    public async Task DisposeAsync() => await ClearAllAsync();

    private async Task ClearAllAsync()
    {
        await Context.PointTransactions.ExecuteDeleteAsync();
        await Context.Customers.ExecuteDeleteAsync();
        await Context.Rewards.ExecuteDeleteAsync();
        Context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Customer_total_points_matches_transaction_sum()
    {
        var customer = CustomersData.Bronze();
        var earned = new[] { 200, 150, 300 };
        var redeemed = new[] { 100, 50 };
        var bonus = new[] { 500 };
        var expired = new[] { 75 };

        foreach (var p in earned.Concat(bonus)) customer.AwardBonus(p, DateTime.UtcNow);
        foreach (var p in redeemed) customer.Redeem(p);
        foreach (var p in expired) customer.ExpirePoints(p);
        await Context.Customers.AddAsync(customer);

        foreach (var p in earned)
            await Context.PointTransactions.AddAsync(PointTransactionsData.Earned(customer.Id, p));
        foreach (var p in redeemed)
            await Context.PointTransactions.AddAsync(PointTransactionsData.Redeemed(customer.Id, p));
        foreach (var p in bonus)
            await Context.PointTransactions.AddAsync(PointTransactionsData.Bonus(customer.Id, p));
        foreach (var p in expired)
            await Context.PointTransactions.AddAsync(PointTransactionsData.Expired(customer.Id, p));
        await SaveChangesAsync();

        var deposits = earned.Concat(bonus).Sum();
        var withdrawals = redeemed.Concat(expired).Sum();
        var refreshed = await Context.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);
        refreshed.TotalPoints.Should().Be(deposits - withdrawals);
    }

    [Fact]
    public async Task Customer_total_earned_points_matches_earned_plus_bonus_sum()
    {
        var customer = CustomersData.Bronze();
        customer.AwardBonus(1_000, DateTime.UtcNow);
        customer.AwardBonus(2_500, DateTime.UtcNow);
        customer.Redeem(500);
        customer.ExpirePoints(200);
        await Context.Customers.AddAsync(customer);
        await SaveChangesAsync();

        var refreshed = await Context.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);
        refreshed.TotalEarnedPoints.Should().Be(3_500);
        refreshed.TotalPoints.Should().Be(2_800);
    }

    [Fact]
    public async Task Concurrent_earn_calls_preserve_consistency()
    {
        var customer = CustomersData.Bronze();
        await Context.Customers.AddAsync(customer);
        await SaveChangesAsync();

        const int parallelism = 20;
        const int basePoints = 10;

        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = Factory.Services.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new EarnPointsCommand
                {
                    CustomerId = customer.Id.Value,
                    BasePoints = basePoints,
                    Description = "concurrent earn"
                });
            }))
            .ToArray();
        await Task.WhenAll(tasks);

        var refreshed = await Context.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);
        refreshed.TotalPoints.Should().Be(parallelism * basePoints);
        refreshed.TotalEarnedPoints.Should().Be(parallelism * basePoints);

        var earnedTxs = await Context.PointTransactions.AsNoTracking()
            .CountAsync(t => t.CustomerId == customer.Id && t.Type == PointTransactionType.Earned);
        earnedTxs.Should().Be(parallelism);
    }
}
