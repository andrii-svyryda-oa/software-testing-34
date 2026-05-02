using Bogus;
using Domain.Customers;

namespace Test.Data.Builders;

public static class CustomersData
{
    private static readonly Faker Faker = new();

    public static Customer Bronze(string? email = null) =>
        Customer.Register(
            CustomerId.New(),
            name: Faker.Name.FullName(),
            email: email ?? Faker.Internet.Email(),
            phone: Faker.Phone.PhoneNumber("+##########"),
            joinDate: DateTime.UtcNow.AddDays(-Faker.Random.Int(0, 365)));

    /// <summary>
    /// Returns a Bronze-registered customer then awards a bonus equal to <paramref name="earned"/>
    /// (no multiplier) to land on the requested total earned points / tier deterministically.
    /// </summary>
    public static Customer WithEarned(int earned, string? email = null)
    {
        var c = Bronze(email);
        if (earned > 0) c.AwardBonus(earned, DateTime.UtcNow);
        return c;
    }
}
