using Application.Customers.Commands;
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
public class RewardStockConcurrencyTests : BaseIntegrationTest, IAsyncLifetime
{
    public RewardStockConcurrencyTests(IntegrationTestWebFactory factory) : base(factory) { }

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
    public async Task Twenty_parallel_redeems_against_stock_of_five_yields_exactly_five_successes()
    {
        // Many customers (one per attempt) so that customer-level concurrency on the
        // shared customer doesn't dominate the test — we want to isolate reward stock.
        const int parallelism = 20;
        const int stock = 5;
        const int rewardCost = 100;

        var customers = Enumerable.Range(0, parallelism).Select(_ =>
        {
            var c = CustomersData.Bronze();
            c.AwardBonus(100_000, DateTime.UtcNow);
            return c;
        }).ToList();
        await Context.Customers.AddRangeAsync(customers);
        foreach (var c in customers)
            await Context.PointTransactions.AddAsync(
                PointTransactionsData.Bonus(c.Id, 100_000, DateTime.UtcNow));

        var reward = RewardsData.Catalog(rewardCost, stock);
        await Context.Rewards.AddAsync(reward);
        await SaveChangesAsync();

        var tasks = customers.Select(c => Task.Run(async () =>
        {
            using var scope = Factory.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new RedeemPointsCommand
            {
                CustomerId = c.Id.Value,
                RewardId = reward.Id.Value
            });
            return result.IsSuccess;
        })).ToArray();

        var outcomes = await Task.WhenAll(tasks);
        outcomes.Count(success => success).Should().Be(stock);

        var refreshedReward = await Context.Rewards.AsNoTracking()
            .FirstAsync(r => r.Id == reward.Id);
        refreshedReward.StockQuantity.Should().Be(0);

        var redeemedRows = await Context.PointTransactions.AsNoTracking()
            .Where(t => t.Type == PointTransactionType.Redeemed
                     && t.Description.Contains(reward.Name))
            .ToListAsync();
        redeemedRows.Should().HaveCount(stock);
        redeemedRows.Sum(t => t.Points).Should().Be(stock * rewardCost);
    }
}
