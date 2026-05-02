using Domain.Rewards;

namespace Application.Rewards.Exceptions;

public abstract class RewardException(RewardId id, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public RewardId RewardId { get; } = id;
}

public class RewardNotFoundException(RewardId id)
    : RewardException(id, $"Reward {id} not found");

public class RewardOutOfStockException(RewardId id)
    : RewardException(id, $"Reward {id} is out of stock");

public class RewardInactiveException(RewardId id)
    : RewardException(id, $"Reward {id} is inactive");

public class RewardUnknownException(RewardId id, Exception inner)
    : RewardException(id, $"Unknown error for reward {id}", inner);
