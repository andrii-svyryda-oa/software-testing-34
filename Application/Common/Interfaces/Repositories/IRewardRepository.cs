using Domain.Rewards;
using Optional;

namespace Application.Common.Interfaces.Repositories;

public interface IRewardRepository
{
    Task<Reward> Add(Reward reward, CancellationToken cancellationToken);
    Task<Reward> Update(Reward reward, CancellationToken cancellationToken);
    Task<Option<Reward>> GetById(RewardId id, CancellationToken cancellationToken);
}
