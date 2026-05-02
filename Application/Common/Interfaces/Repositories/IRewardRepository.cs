using Domain.Rewards;
using Optional;

namespace Application.Common.Interfaces.Repositories;

/// <summary>
/// Write-side repository for <see cref="Reward"/>. <c>Add</c> and <c>Update</c> stage changes;
/// callers persist via <see cref="Application.Common.Interfaces.IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface IRewardRepository
{
    Task<Reward> Add(Reward reward, CancellationToken cancellationToken);
    Reward Update(Reward reward);
    Task<Option<Reward>> GetById(RewardId id, CancellationToken cancellationToken);
}
