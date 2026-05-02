namespace Domain.Rewards;

public record RewardId(Guid Value)
{
    public static RewardId New() => new(Guid.NewGuid());
    public static RewardId Empty() => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
