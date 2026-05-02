namespace Domain.Customers;

public static class TierLevels
{
    public const int BronzeThreshold = 0;
    public const int SilverThreshold = 1_000;
    public const int GoldThreshold = 5_000;
    public const int PlatinumThreshold = 10_000;

    public static TierLevel FromTotalEarnedPoints(int totalEarned) => totalEarned switch
    {
        >= PlatinumThreshold => TierLevel.Platinum,
        >= GoldThreshold => TierLevel.Gold,
        >= SilverThreshold => TierLevel.Silver,
        _ => TierLevel.Bronze
    };

    public static decimal MultiplierFor(TierLevel tier) => tier switch
    {
        TierLevel.Platinum => 2.0m,
        TierLevel.Gold => 1.5m,
        _ => 1.0m
    };

    public static (TierLevel next, int pointsToNext) ProgressFrom(int totalEarned)
    {
        if (totalEarned < SilverThreshold) return (TierLevel.Silver, SilverThreshold - totalEarned);
        if (totalEarned < GoldThreshold) return (TierLevel.Gold, GoldThreshold - totalEarned);
        if (totalEarned < PlatinumThreshold) return (TierLevel.Platinum, PlatinumThreshold - totalEarned);
        return (TierLevel.Platinum, 0);
    }
}
