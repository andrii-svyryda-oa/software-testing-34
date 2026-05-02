using Domain.PointTransactions;

namespace Application.PointTransactions.Exceptions;

public abstract class PointTransactionException(PointTransactionId id, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public PointTransactionId TransactionId { get; } = id;
}

public class PointTransactionUnknownException(PointTransactionId id, Exception inner)
    : PointTransactionException(id, $"Unknown error for point transaction {id}", inner);
