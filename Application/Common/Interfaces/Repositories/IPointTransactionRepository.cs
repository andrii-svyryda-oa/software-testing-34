using Domain.Customers;
using Domain.PointTransactions;

namespace Application.Common.Interfaces.Repositories;

public interface IPointTransactionRepository
{
    Task<PointTransaction> Add(PointTransaction transaction, CancellationToken cancellationToken);
    Task<List<PointTransaction>> AddMany(List<PointTransaction> transactions, CancellationToken cancellationToken);

    /// <summary>
    /// Returns Earned/Bonus transactions for the customer that still have <c>Remaining &gt; 0</c>
    /// and have not yet expired against <see cref="DateTime.UtcNow"/>, oldest first.
    /// </summary>
    Task<List<PointTransaction>> GetActiveEarnedFor(CustomerId customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns Earned/Bonus transactions for the customer with <c>Remaining &gt; 0</c>
    /// and <c>CreatedAt &lt;= threshold</c> (i.e. eligible to be expired), oldest first.
    /// </summary>
    Task<List<PointTransaction>> GetExpirableEarnedFor(CustomerId customerId, DateTime threshold, CancellationToken cancellationToken);
}
