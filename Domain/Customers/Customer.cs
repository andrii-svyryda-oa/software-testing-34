namespace Domain.Customers;

/// <summary>
/// Loyalty-program customer.
/// <para>
/// <see cref="TotalEarnedPoints"/> is the cumulative sum of <c>Earned + Bonus</c> point amounts
/// (after multiplier), never decreasing. It drives tier calculation.
/// <see cref="TotalPoints"/> is the redeemable balance: earned + bonus minus redeemed minus expired.
/// </para>
/// </summary>
public class Customer
{
    public CustomerId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public TierLevel TierLevel { get; private set; }
    public int TotalPoints { get; private set; }
    public int TotalEarnedPoints { get; private set; }
    public DateTime JoinDate { get; private set; }

    private Customer() { }

    public static Customer Register(
        CustomerId id,
        string name,
        string email,
        string phone,
        DateTime joinDate)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("Phone is required.", nameof(phone));

        return new Customer
        {
            Id = id,
            Name = name,
            Email = email,
            Phone = phone,
            TierLevel = TierLevel.Bronze,
            TotalPoints = 0,
            TotalEarnedPoints = 0,
            JoinDate = joinDate
        };
    }

    /// <summary>
    /// Adds points to the customer applying the multiplier of the <em>current</em> tier
    /// (computed before the update). Returns the awarded amount.
    /// </summary>
    public int Earn(int basePoints, DateTime at)
    {
        if (basePoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(basePoints), "Base points must be positive.");

        var multiplier = TierLevels.MultiplierFor(TierLevel);
        var awarded = (int)Math.Round(basePoints * multiplier, MidpointRounding.AwayFromZero);

        TotalPoints += awarded;
        TotalEarnedPoints += awarded;
        TierLevel = TierLevels.FromTotalEarnedPoints(TotalEarnedPoints);

        return awarded;
    }

    /// <summary>
    /// Awards a bonus (no multiplier), affecting both balances. Tier may rise.
    /// </summary>
    public void AwardBonus(int points, DateTime at)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Bonus points must be positive.");

        TotalPoints += points;
        TotalEarnedPoints += points;
        TierLevel = TierLevels.FromTotalEarnedPoints(TotalEarnedPoints);
    }

    public void Redeem(int points)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Redeem amount must be positive.");
        if (points > TotalPoints)
            throw new InvalidOperationException(
                $"Cannot redeem {points} points; balance is {TotalPoints}.");

        TotalPoints -= points;
    }

    public void ExpirePoints(int points)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Expire amount must be positive.");
        if (points > TotalPoints)
            throw new InvalidOperationException(
                $"Cannot expire {points} points; balance is {TotalPoints}.");

        TotalPoints -= points;
    }
}
