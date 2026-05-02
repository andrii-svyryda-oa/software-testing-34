using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Optional;

namespace Infrastructure.Persistence.Repositories;

public class RewardRepository(ApplicationDbContext context) : IRewardRepository, IRewardQueries
{
    public async Task<Option<Reward>> GetById(RewardId id, CancellationToken cancellationToken)
    {
        var entity = await context.Rewards
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? Option.None<Reward>() : Option.Some(entity);
    }

    public async Task<IReadOnlyList<Reward>> GetAvailable(CancellationToken cancellationToken)
    {
        return await context.Rewards
            .AsNoTracking()
            .Where(x => x.IsActive && x.StockQuantity > 0)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Reward> Add(Reward reward, CancellationToken cancellationToken)
    {
        await context.Rewards.AddAsync(reward, cancellationToken);
        return reward;
    }

    public Reward Update(Reward reward)
    {
        context.Rewards.Update(reward);
        return reward;
    }
}
