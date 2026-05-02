namespace Domain.PointTransactions;

public record PointTransactionId(Guid Value)
{
    public static PointTransactionId New() => new(Guid.NewGuid());
    public static PointTransactionId Empty() => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
