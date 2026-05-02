using AutoFixture;
using Domain.Customers;
using Domain.PointTransactions;
using Domain.Rewards;
using Test.Data.Builders;

namespace Test.Data.AutoFixture;

/// <summary>
/// AutoFixture customization that returns Loyalty-domain entities built via the
/// invariant-respecting factories (so business-rule fields are valid). The fixture
/// users still set tier-driven values explicitly when they matter.
/// </summary>
public class LoyaltyCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Register(() => CustomersData.Bronze());
        fixture.Register(() => RewardsData.Catalog(
            pointsCost: 100, stock: 10));
        fixture.Register(() => PointTransactionsData.Earned(
            CustomerId.New(), points: 100));
        fixture.Register(() => CustomerId.New());
        fixture.Register(() => RewardId.New());
        fixture.Register(() => PointTransactionId.New());
    }
}
