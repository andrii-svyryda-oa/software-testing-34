using Bogus;
using Domain.Customers;
using Domain.PointTransactions;

namespace Test.Data.Builders;

public static class PointTransactionsData
{
    private static readonly Faker Faker = new();

    public static PointTransaction Earned(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Earned(
            PointTransactionId.New(), cid, points,
            description: Faker.Commerce.Department(),
            at: at ?? DateTime.UtcNow);

    public static PointTransaction Redeemed(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Redeemed(
            PointTransactionId.New(), cid, points,
            description: $"Redemption {Faker.Random.AlphaNumeric(8)}",
            at: at ?? DateTime.UtcNow);

    public static PointTransaction Bonus(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Bonus(
            PointTransactionId.New(), cid, points,
            description: "Bonus",
            at: at ?? DateTime.UtcNow);

    public static PointTransaction Expired(CustomerId cid, int points, DateTime? at = null) =>
        PointTransaction.Expired(
            PointTransactionId.New(), cid, points,
            description: "Expired",
            at: at ?? DateTime.UtcNow);
}
