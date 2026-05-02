using System.Text.Json;
using Domain.Customers;
using Domain.PointTransactions;
using Domain.Rewards;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Test.Data.Seeders;

namespace Api;

/// <summary>
/// Bootstrap routine invoked by <c>dotnet run --project Api -- --seed</c> for the perf
/// pipeline. Resets the database to a known state of >=10 000 records (via
/// <see cref="LoyaltySeeder"/>), inserts a deliberately scarce reward, and writes the GUIDs
/// used by the k6 scripts to disk.
/// </summary>
public static class PerfSeed
{
    public static async Task RunAsync(WebApplication app, string[] args)
    {
        var outputDirectory = ParseOption(args, "--output", "perf");
        Directory.CreateDirectory(outputDirectory);

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync();

        var summary = await LoyaltySeeder.SeedAsync(context, targetTotal: 10_000, seed: 42);

        var customerIds = await context.Customers.AsNoTracking()
            .Select(c => c.Id.Value).ToListAsync();
        var richCustomerIds = await context.Customers.AsNoTracking()
            .Where(c => c.TotalPoints > 100_000)
            .Select(c => c.Id.Value)
            .ToListAsync();

        // If insufficient rich customers exist (the seeder distributes deposits across many
        // smaller transactions), top up some up to >100k so the redeem-stress script always
        // has eligible candidates.
        if (richCustomerIds.Count < 50)
        {
            var topUpTargets = await context.Customers
                .OrderByDescending(c => c.TotalPoints)
                .Take(50 - richCustomerIds.Count)
                .ToListAsync();
            foreach (var c in topUpTargets)
            {
                var topUp = 200_000;
                c.AwardBonus(topUp, DateTime.UtcNow);
                await context.PointTransactions.AddAsync(
                    PointTransaction.Bonus(PointTransactionId.New(), c.Id, topUp,
                        "Perf bootstrap top-up", DateTime.UtcNow));
            }
            await context.SaveChangesAsync();
            richCustomerIds = await context.Customers.AsNoTracking()
                .Where(c => c.TotalPoints > 100_000)
                .Select(c => c.Id.Value)
                .ToListAsync();
        }

        // Insert the scarce reward consumed by redeem-stress.js. Stock = 50.
        var scarceReward = Reward.Create(
            RewardId.New(),
            name: "Perf Scarce Reward",
            description: "Deliberately scarce reward for redeem-stress perf tests.",
            pointsCost: 100,
            category: "Perf",
            stockQuantity: 50);
        await context.Rewards.AddAsync(scarceReward);
        await context.SaveChangesAsync();

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "customer-ids.json"),
            JsonSerializer.Serialize(customerIds));
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "rich-customer-ids.json"),
            JsonSerializer.Serialize(richCustomerIds));

        Console.Out.WriteLine(scarceReward.Id.Value);
        await Console.Out.FlushAsync();

        Console.Error.WriteLine(
            $"Seeded {summary.Total} records " +
            $"({summary.Customers} customers, {summary.Rewards} rewards, " +
            $"{summary.Transactions} transactions). " +
            $"Rich customers: {richCustomerIds.Count}. " +
            $"Scarce reward GUID emitted on stdout.");
    }

    private static string ParseOption(string[] args, string name, string defaultValue)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : defaultValue;
    }
}
