using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Domain.Customers;
using Domain.PointTransactions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PointTransactionRepository(ApplicationDbContext context)
    : IPointTransactionRepository, IPointTransactionQueries
{
    public async Task<PointTransaction> Add(PointTransaction transaction, CancellationToken cancellationToken)
    {
        await context.PointTransactions.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<List<PointTransaction>> AddMany(List<PointTransaction> transactions, CancellationToken cancellationToken)
    {
        await context.PointTransactions.AddRangeAsync(transactions, cancellationToken);
        return transactions;
    }

    public async Task<List<PointTransaction>> GetActiveEarnedFor(CustomerId customerId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var threshold = now.AddMonths(-12);
        return await context.PointTransactions
            .Where(x => x.CustomerId == customerId
                && (x.Type == PointTransactionType.Earned || x.Type == PointTransactionType.Bonus)
                && x.Remaining > 0
                && x.CreatedAt > threshold)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PointTransaction>> GetExpirableEarnedFor(
        CustomerId customerId, DateTime threshold, CancellationToken cancellationToken)
    {
        return await context.PointTransactions
            .Where(x => x.CustomerId == customerId
                && (x.Type == PointTransactionType.Earned || x.Type == PointTransactionType.Bonus)
                && x.Remaining > 0
                && x.CreatedAt <= threshold)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PointTransaction>> GetHistoryFor(
        CustomerId customerId, int skip, int take, CancellationToken cancellationToken)
    {
        return await context.PointTransactions
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountFor(CustomerId customerId, CancellationToken cancellationToken)
    {
        return context.PointTransactions
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .CountAsync(cancellationToken);
    }
}
