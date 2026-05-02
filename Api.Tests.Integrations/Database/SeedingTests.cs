using Domain.PointTransactions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.Data.Seeders;
using Tests.Common;
using Xunit;

namespace Api.Tests.Integrations.Database;

[Collection("IntegrationTests")]
public class SeedingTests : BaseIntegrationTest, IAsyncLifetime
{
    public SeedingTests(IntegrationTestWebFactory factory) : base(factory) { }

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
    public async Task Seeder_produces_at_least_10000_records()
    {
        var summary = await LoyaltySeeder.SeedAsync(Context, targetTotal: 10_000, seed: 42);
        summary.Total.Should().BeGreaterThanOrEqualTo(10_000);

        var customers = await Context.Customers.AsNoTracking().CountAsync();
        var rewards = await Context.Rewards.AsNoTracking().CountAsync();
        var transactions = await Context.PointTransactions.AsNoTracking().CountAsync();
        (customers + rewards + transactions).Should().BeGreaterThanOrEqualTo(10_000);
    }

    [Fact]
    public async Task Seeder_is_internally_consistent()
    {
        await LoyaltySeeder.SeedAsync(Context, targetTotal: 10_000, seed: 42);

        var customers = await Context.Customers.AsNoTracking().ToListAsync();
        var transactions = await Context.PointTransactions.AsNoTracking().ToListAsync();

        var byCustomer = transactions
            .GroupBy(t => t.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var customer in customers)
        {
            if (!byCustomer.TryGetValue(customer.Id, out var txs)) continue;

            var earned = txs.Where(t => t.Type is PointTransactionType.Earned or PointTransactionType.Bonus).Sum(t => t.Points);
            var withdrawn = txs.Where(t => t.Type is PointTransactionType.Redeemed or PointTransactionType.Expired).Sum(t => t.Points);

            customer.TotalEarnedPoints.Should().Be(earned, "TotalEarnedPoints should equal sum of Earned+Bonus");
            customer.TotalPoints.Should().Be(earned - withdrawn, "TotalPoints should equal earned minus withdrawn");
        }
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        var first = await LoyaltySeeder.SeedAsync(Context, targetTotal: 10_000, seed: 42);
        var second = await LoyaltySeeder.SeedAsync(Context, targetTotal: 10_000, seed: 42);

        second.Customers.Should().Be(first.Customers);
        second.Rewards.Should().Be(first.Rewards);
        second.Transactions.Should().Be(first.Transactions);
    }

    [Fact]
    public async Task Seeder_is_deterministic_with_fixed_seed()
    {
        await LoyaltySeeder.SeedAsync(Context, targetTotal: 10_000, seed: 42);
        var sumA = await Context.PointTransactions.AsNoTracking()
            .GroupBy(t => t.Type)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.Points) })
            .ToListAsync();

        await LoyaltySeeder.SeedAsync(Context, targetTotal: 10_000, seed: 42);
        var sumB = await Context.PointTransactions.AsNoTracking()
            .GroupBy(t => t.Type)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.Points) })
            .ToListAsync();

        sumA.OrderBy(x => x.Key).Should().BeEquivalentTo(sumB.OrderBy(x => x.Key));
    }
}
