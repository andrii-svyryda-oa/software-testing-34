using Application.Common;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Repositories;
using Application.Customers.Exceptions;
using Domain.Customers;
using Domain.PointTransactions;
using MediatR;

namespace Application.Customers.Commands;

public record EarnPointsCommand : IRequest<Result<Customer, CustomerException>>
{
    public required Guid CustomerId { get; init; }
    public required int BasePoints { get; init; }
    public string Description { get; init; } = "Points earned";
}

/// <summary>
/// Earns points for a customer, applying the tier multiplier (Gold x1.5, Platinum x2).
/// Both the updated customer and the new <see cref="PointTransaction"/> are persisted in
/// a single <see cref="IUnitOfWork.SaveChangesAsync"/> call so the writes are atomic.
/// Retries on optimistic concurrency conflicts (xmin token mismatch) so concurrent
/// earn calls on the same customer do not lose updates.
/// </summary>
public class EarnPointsCommandHandler(
    ICustomerRepository customerRepository,
    IPointTransactionRepository transactionRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<EarnPointsCommand, Result<Customer, CustomerException>>
{
    private const int MaxConcurrencyRetries = 64;

    public async Task<Result<Customer, CustomerException>> Handle(
        EarnPointsCommand request, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(request.CustomerId);

        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            var maybe = await customerRepository.GetById(customerId, cancellationToken);
            if (!maybe.HasValue)
                return new CustomerNotFoundException(customerId);

            var customer = maybe.ValueOr(default(Customer)!);
            try
            {
                var at = DateTime.UtcNow;
                var awarded = customer.Earn(request.BasePoints, at);

                var tx = PointTransaction.Earned(
                    PointTransactionId.New(),
                    customer.Id,
                    awarded,
                    request.Description,
                    at);

                customerRepository.Update(customer);
                await transactionRepository.Add(tx, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return customer;
            }
            catch (ConcurrencyConflictException)
            {
                // Another writer raced us. Discard the stale tracked entity and reload.
                await unitOfWork.DiscardTrackedChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return new CustomerUnknownException(customerId, ex);
            }
        }

        return new CustomerUnknownException(customerId,
            new InvalidOperationException(
                $"Could not earn points after {MaxConcurrencyRetries} concurrency retries."));
    }
}
