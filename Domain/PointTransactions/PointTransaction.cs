using Domain.Customers;

namespace Domain.PointTransactions;

/// <summary>
/// Immutable record of a points event. <see cref="Remaining"/> is mutable but only via
/// <see cref="Consume"/>; it tracks the unconsumed portion of an Earned/Bonus balance for
/// FIFO redemption / expiration. For Redeemed/Expired transactions <see cref="Remaining"/>
/// is always 0 and immutable.
/// </summary>
public class PointTransaction
{
    public PointTransactionId Id { get; private set; } = null!;
    public CustomerId CustomerId { get; private set; } = null!;
    public int Points { get; private set; }
    public int Remaining { get; private set; }
    public PointTransactionType Type { get; private set; }
    public string Description { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private PointTransaction() { }

    public static PointTransaction Earned(
        PointTransactionId id, CustomerId customerId, int points, string description, DateTime at)
        => Create(id, customerId, points, PointTransactionType.Earned, description, at, remaining: points);

    public static PointTransaction Redeemed(
        PointTransactionId id, CustomerId customerId, int points, string description, DateTime at)
        => Create(id, customerId, points, PointTransactionType.Redeemed, description, at, remaining: 0);

    public static PointTransaction Expired(
        PointTransactionId id, CustomerId customerId, int points, string description, DateTime at)
        => Create(id, customerId, points, PointTransactionType.Expired, description, at, remaining: 0);

    public static PointTransaction Bonus(
        PointTransactionId id, CustomerId customerId, int points, string description, DateTime at)
        => Create(id, customerId, points, PointTransactionType.Bonus, description, at, remaining: points);

    private static PointTransaction Create(
        PointTransactionId id,
        CustomerId customerId,
        int points,
        PointTransactionType type,
        string description,
        DateTime at,
        int remaining)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(customerId);
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be positive.");
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        return new PointTransaction
        {
            Id = id,
            CustomerId = customerId,
            Points = points,
            Remaining = remaining,
            Type = type,
            Description = description,
            CreatedAt = at
        };
    }

    public bool IsExpired(DateTime now)
        => (Type is PointTransactionType.Earned or PointTransactionType.Bonus)
           && now >= CreatedAt.AddMonths(12);

    /// <summary>
    /// Reduces <see cref="Remaining"/> by <paramref name="amount"/>. Only valid for Earned/Bonus.
    /// </summary>
    public void Consume(int amount)
    {
        if (Type is not (PointTransactionType.Earned or PointTransactionType.Bonus))
            throw new InvalidOperationException(
                $"Only Earned/Bonus transactions can be consumed; this is {Type}.");
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Consume amount must be positive.");
        if (amount > Remaining)
            throw new InvalidOperationException(
                $"Cannot consume {amount}; remaining is {Remaining}.");

        Remaining -= amount;
    }
}
