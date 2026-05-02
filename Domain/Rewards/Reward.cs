namespace Domain.Rewards;

public class Reward
{
    public RewardId Id { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public int PointsCost { get; private set; }
    public string Category { get; private set; } = null!;
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }

    private Reward() { }

    public static Reward Create(
        RewardId id,
        string name,
        string description,
        int pointsCost,
        string category,
        int stockQuantity)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
        if (pointsCost <= 0) throw new ArgumentOutOfRangeException(nameof(pointsCost), "PointsCost must be positive.");
        if (stockQuantity < 0) throw new ArgumentOutOfRangeException(nameof(stockQuantity), "StockQuantity cannot be negative.");

        return new Reward
        {
            Id = id,
            Name = name,
            Description = description,
            PointsCost = pointsCost,
            Category = category,
            StockQuantity = stockQuantity,
            IsActive = true
        };
    }

    public void Decrement()
    {
        if (StockQuantity == 0)
            throw new InvalidOperationException($"Reward {Id} is out of stock.");
        StockQuantity--;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public bool IsAvailable() => IsActive && StockQuantity > 0;
}
