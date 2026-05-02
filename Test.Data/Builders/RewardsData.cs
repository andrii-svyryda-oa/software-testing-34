using Bogus;
using Domain.Rewards;

namespace Test.Data.Builders;

public static class RewardsData
{
    private static readonly Faker Faker = new();

    public static Reward Catalog(int pointsCost, int stock) =>
        Reward.Create(
            RewardId.New(),
            name: $"{Faker.Commerce.ProductName()} {Faker.Random.AlphaNumeric(4)}",
            description: Faker.Commerce.ProductDescription(),
            pointsCost: pointsCost,
            category: Faker.Commerce.Categories(1)[0],
            stockQuantity: stock);

    public static Reward Inactive(int pointsCost = 100)
    {
        var r = Catalog(pointsCost, stock: 10);
        r.Deactivate();
        return r;
    }

    public static Reward OutOfStock(int pointsCost = 100) => Catalog(pointsCost, stock: 0);
}
