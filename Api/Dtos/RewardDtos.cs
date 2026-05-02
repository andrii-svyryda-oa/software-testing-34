using Domain.Rewards;

namespace Api.Dtos;

public record RewardDto(
    Guid Id,
    string Name,
    string Description,
    int PointsCost,
    string Category,
    int StockQuantity,
    bool IsActive)
{
    public static RewardDto FromDomainModel(Reward r) => new(
        r.Id.Value, r.Name, r.Description, r.PointsCost,
        r.Category, r.StockQuantity, r.IsActive);
}

public record CreateRewardDto(
    string Name,
    string Description,
    int PointsCost,
    string Category,
    int StockQuantity);
