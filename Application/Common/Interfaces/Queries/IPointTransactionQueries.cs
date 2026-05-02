using Domain.Customers;
using Domain.PointTransactions;

namespace Application.Common.Interfaces.Queries;

public interface IPointTransactionQueries
{
    /// <summary>
    /// Paginated transaction history for a customer, ordered most-recent first.
    /// </summary>
    Task<IReadOnlyList<PointTransaction>> GetHistoryFor(
        CustomerId customerId, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountFor(CustomerId customerId, CancellationToken cancellationToken);
}
