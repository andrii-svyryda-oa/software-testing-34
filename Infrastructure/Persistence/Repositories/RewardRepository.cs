using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Domain.Rewards;
using Microsoft.EntityFrameworkCore;
using Optional;

namespace Infrastructure.Persistence.Repositories;

public class RewardRepository(ApplicationDbContext context) : IRewardRepository, IRewardQueries
{
    /// <summary>
    /// Write-side <c>GetById</c>: returns a tracked entity so that subsequent calls to
    /// <see cref="Update"/> + <see cref="ApplicationDbContext.SaveChangesAsync(CancellationToken)"/>
    /// preserve the original <c>xmin</c> concurrency token.
    /// </summary>
    Task<Option<Reward>> IRewardRepository.GetById(RewardId id, CancellationToken cancellationToken)
        => GetByIdTracked(id, cancellationToken);

    /// <summary>
    /// Read-side <c>GetById</c>: untracked.
    /// </summary>
    Task<Option<Reward>> IRewardQueries.GetById(RewardId id, CancellationToken cancellationToken)
        => GetByIdAsNoTracking(id, cancellationToken);

    private async Task<Option<Reward>> GetByIdTracked(RewardId id, CancellationToken ct)
    {
        var entity = await context.Rewards.FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? Option.None<Reward>() : Option.Some(entity);
    }

    private async Task<Option<Reward>> GetByIdAsNoTracking(RewardId id, CancellationToken ct)
    {
        var entity = await context.Rewards.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
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
        // Tracked entities are already in Modified state after domain mutations; calling
        // Update is a no-op for them and ensures detached entities still get persisted.
        var entry = context.Entry(reward);
        if (entry.State == EntityState.Detached)
        {
            context.Rewards.Update(reward);
        }
        return reward;
    }
}
