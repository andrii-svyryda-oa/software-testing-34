using Domain.Rewards;
using Optional;

namespace Application.Common.Interfaces.Queries;

public interface IRewardQueries
{
    /// <summary>
    /// Returns rewards with <c>IsActive == true &amp;&amp; StockQuantity &gt; 0</c>, ordered by name.
    /// </summary>
    Task<IReadOnlyList<Reward>> GetAvailable(CancellationToken cancellationToken);
    Task<Option<Reward>> GetById(RewardId id, CancellationToken cancellationToken);
}
