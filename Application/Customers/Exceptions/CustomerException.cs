using Domain.Customers;
using Domain.Rewards;

namespace Application.Customers.Exceptions;

public abstract class CustomerException(CustomerId id, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public CustomerId CustomerId { get; } = id;
}

public class CustomerNotFoundException(CustomerId id)
    : CustomerException(id, $"Customer {id} not found");

public class CustomerAlreadyExistsException(CustomerId id)
    : CustomerException(id, $"Customer with this email already exists ({id})");

public class InsufficientPointsException(CustomerId id, int requested, int available)
    : CustomerException(id, $"Customer {id} has only {available} redeemable points; requested {requested}")
{
    public int Requested { get; } = requested;
    public int Available { get; } = available;
}

public class CustomerUnknownException(CustomerId id, Exception inner)
    : CustomerException(id, $"Unknown error for customer {id}", inner);

public class RedeemRewardOutOfStockException(CustomerId id, RewardId rewardId)
    : CustomerException(id, $"Reward {rewardId} is out of stock")
{
    public RewardId RewardId { get; } = rewardId;
}

public class RedeemRewardInactiveException(CustomerId id, RewardId rewardId)
    : CustomerException(id, $"Reward {rewardId} is inactive")
{
    public RewardId RewardId { get; } = rewardId;
}

public class RedeemRewardNotFoundException(CustomerId id, RewardId rewardId)
    : CustomerException(id, $"Reward {rewardId} not found")
{
    public RewardId RewardId { get; } = rewardId;
}
