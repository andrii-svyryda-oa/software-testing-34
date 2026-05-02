using Application.Common;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Application.Customers.Exceptions;
using Domain.Customers;
using Domain.PointTransactions;
using MediatR;

namespace Application.PointTransactions.Commands;

public record ExpirePointsCommand : IRequest<Result<int, CustomerException>>
{
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// "Now" injected by the caller (kept out of <see cref="DateTime.UtcNow"/> for testability).
    /// Transactions older than <c>At - 12 months</c> with <c>Remaining &gt; 0</c> are expired.
    /// </summary>
    public required DateTime At { get; init; }
}

/// <summary>
/// Expires Earned/Bonus point transactions older than 12 months for a customer (FIFO).
/// Idempotent: a second call with the same <see cref="ExpirePointsCommand.At"/> returns 0
/// because consumed/expired transactions have <c>Remaining = 0</c> and are skipped.
/// </summary>
public class ExpirePointsCommandHandler(
    ICustomerQueries customerQueries,
    ICustomerRepository customerRepository,
    IPointTransactionRepository transactionRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ExpirePointsCommand, Result<int, CustomerException>>
{
    public async Task<Result<int, CustomerException>> Handle(
        ExpirePointsCommand request, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(request.CustomerId);
        var maybe = await customerQueries.GetById(customerId, cancellationToken);
        if (!maybe.HasValue)
            return new CustomerNotFoundException(customerId);

        var customer = maybe.ValueOr(default(Customer)!);

        try
        {
            var threshold = request.At.AddMonths(-12);
            var expirable = await transactionRepository.GetExpirableEarnedFor(
                customerId, threshold, cancellationToken);

            var totalExpired = 0;
            foreach (var tx in expirable)
            {
                if (tx.Remaining <= 0) continue;
                var amount = tx.Remaining;
                tx.Consume(amount);
                totalExpired += amount;
            }

            if (totalExpired == 0) return 0;

            customer.ExpirePoints(totalExpired);

            var record = PointTransaction.Expired(
                PointTransactionId.New(),
                customer.Id,
                totalExpired,
                "Points expired (>12 months)",
                request.At);

            customerRepository.Update(customer);
            await transactionRepository.Add(record, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return totalExpired;
        }
        catch (Exception ex)
        {
            return new CustomerUnknownException(customerId, ex);
        }
    }
}
