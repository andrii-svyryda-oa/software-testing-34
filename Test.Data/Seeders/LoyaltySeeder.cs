using Bogus;
using Domain.Customers;
using Domain.PointTransactions;
using Domain.Rewards;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Test.Data.Builders;

namespace Test.Data.Seeders;

/// <summary>
/// Bulk-loads the loyalty database with an internally consistent dataset of ≥10 000 records
/// distributed across customers / rewards / point transactions. Used by integration and
/// performance tests.
/// </summary>
public static class LoyaltySeeder
{
    public record SeedSummary(int Customers, int Rewards, int Transactions)
    {
        public int Total => Customers + Rewards + Transactions;
    }

    public static async Task<SeedSummary> SeedAsync(
        ApplicationDbContext context,
        int targetTotal = 10_000,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        await ClearAsync(context, cancellationToken);

        var faker = seed is null ? new Faker() : new Faker { Random = new Randomizer(seed.Value) };

        const int customerCount = 500;
        const int rewardCount = 50;
        var transactionCount = Math.Max(targetTotal - customerCount - rewardCount, 0);

        var customers = BuildCustomers(faker, customerCount);
        var rewards = BuildRewards(faker, rewardCount);
        var transactions = BuildTransactions(faker, customers, transactionCount);

        await context.Customers.AddRangeAsync(customers, cancellationToken);
        await context.Rewards.AddRangeAsync(rewards, cancellationToken);
        await context.PointTransactions.AddRangeAsync(transactions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new SeedSummary(customers.Count, rewards.Count, transactions.Count);
    }

    private static async Task ClearAsync(ApplicationDbContext context, CancellationToken ct)
    {
        await context.PointTransactions.ExecuteDeleteAsync(ct);
        await context.Rewards.ExecuteDeleteAsync(ct);
        await context.Customers.ExecuteDeleteAsync(ct);
    }

    private static List<Customer> BuildCustomers(Faker faker, int count)
    {
        var customers = new List<Customer>(count);
        for (var i = 0; i < count; i++)
        {
            var email = $"seed_{Guid.NewGuid():N}@loyalty.test";
            var c = Customer.Register(
                CustomerId.New(),
                name: faker.Name.FullName(),
                email: email,
                phone: faker.Phone.PhoneNumber("+##########"),
                joinDate: DateTime.UtcNow.AddDays(-faker.Random.Int(180, 720)));
            customers.Add(c);
        }
        return customers;
    }

    private static List<Reward> BuildRewards(Faker faker, int count)
    {
        var rewards = new List<Reward>(count);
        for (var i = 0; i < count; i++)
        {
            var pointsCost = faker.Random.Int(100, 5_000);
            var bucket = faker.Random.Double();
            int stock;
            bool deactivate;
            if (bucket < 0.80) { stock = faker.Random.Int(1, 100); deactivate = false; }
            else if (bucket < 0.95) { stock = 0; deactivate = false; }
            else { stock = faker.Random.Int(1, 100); deactivate = true; }

            var r = Reward.Create(
                RewardId.New(),
                name: $"{faker.Commerce.ProductName()} {i}",
                description: faker.Commerce.ProductDescription(),
                pointsCost: pointsCost,
                category: faker.Commerce.Categories(1)[0],
                stockQuantity: stock);

            if (deactivate) r.Deactivate();
            rewards.Add(r);
        }
        return rewards;
    }

    /// <summary>
    /// Generates ≥<paramref name="targetCount"/> point transactions distributed across the
    /// supplied customers. For each customer the algorithm:
    ///   1. Generates a sequence of Earned/Bonus deposits with random amounts and CreatedAt
    ///      spread across the past 18 months, awarding points to the customer (no multiplier
    ///      — direct deposit semantics) so their <see cref="Customer.TotalEarnedPoints"/> tracks
    ///      exactly the sum of deposits.
    ///   2. Optionally generates a few Redeemed/Expired transactions that consume the earliest
    ///      deposits' <see cref="PointTransaction.Remaining"/>, mirroring the redeem/expire
    ///      handler logic. <see cref="Customer.TotalPoints"/> is decremented to match.
    /// The result is a customer state that is consistent with the transactions emitted.
    /// </summary>
    private static List<PointTransaction> BuildTransactions(
        Faker faker, List<Customer> customers, int targetCount)
    {
        var txs = new List<PointTransaction>(targetCount);
        var perCustomer = Math.Max(1, targetCount / customers.Count);

        foreach (var customer in customers)
        {
            // Slight per-customer variance so we don't end up with everyone at exactly perCustomer.
            var thisCount = Math.Max(1, perCustomer + faker.Random.Int(-3, 3));
            var deposits = new List<PointTransaction>();
            var depositCount = (int)Math.Round(thisCount * 0.70);
            var redeemCount = (int)Math.Round(thisCount * 0.25);
            var expireCount = thisCount - depositCount - redeemCount;

            for (var i = 0; i < depositCount; i++)
            {
                var at = DateTime.UtcNow.AddDays(-faker.Random.Int(0, 540));
                var points = faker.Random.Int(50, 500);
                var isBonus = faker.Random.Double() < 0.15;
                var tx = isBonus
                    ? PointTransactionsData.Bonus(customer.Id, points, at)
                    : PointTransactionsData.Earned(customer.Id, points, at);
                deposits.Add(tx);
                customer.AwardBonus(points, at);
            }

            deposits.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
            txs.AddRange(deposits);

            for (var i = 0; i < redeemCount; i++)
            {
                if (customer.TotalPoints == 0) break;
                var available = TotalRemaining(deposits);
                if (available == 0) break;

                var amount = faker.Random.Int(10, Math.Max(10, Math.Min(available, 200)));
                amount = Math.Min(amount, customer.TotalPoints);
                if (amount == 0) break;

                ConsumeOldest(deposits, amount);
                customer.Redeem(amount);
                txs.Add(PointTransactionsData.Redeemed(customer.Id, amount,
                    DateTime.UtcNow.AddDays(-faker.Random.Int(0, 90))));
            }

            for (var i = 0; i < expireCount; i++)
            {
                var available = TotalRemaining(deposits);
                if (available == 0) break;
                var amount = faker.Random.Int(10, Math.Max(10, Math.Min(available, 100)));
                amount = Math.Min(amount, customer.TotalPoints);
                if (amount == 0) break;

                ConsumeOldest(deposits, amount);
                customer.ExpirePoints(amount);
                txs.Add(PointTransactionsData.Expired(customer.Id, amount,
                    DateTime.UtcNow.AddDays(-faker.Random.Int(0, 30))));
            }
        }

        return txs;
    }

    private static int TotalRemaining(IEnumerable<PointTransaction> deposits)
        => deposits.Sum(d => d.Remaining);

    private static void ConsumeOldest(List<PointTransaction> deposits, int amount)
    {
        foreach (var d in deposits)
        {
            if (amount == 0) break;
            if (d.Remaining <= 0) continue;
            var take = Math.Min(d.Remaining, amount);
            d.Consume(take);
            amount -= take;
        }
    }
}
