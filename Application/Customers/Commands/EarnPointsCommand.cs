using Application.Common;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Queries;
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
/// Earns points for a customer, applying the tier multiplier (Gold ×1.5, Platinum ×2).
/// Both the updated customer and the new <see cref="PointTransaction"/> are persisted in
/// a single <see cref="IUnitOfWork.SaveChangesAsync"/> call so the writes are atomic.
/// </summary>
public class EarnPointsCommandHandler(
    ICustomerQueries customerQueries,
    ICustomerRepository customerRepository,
    IPointTransactionRepository transactionRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<EarnPointsCommand, Result<Customer, CustomerException>>
{
    public async Task<Result<Customer, CustomerException>> Handle(
        EarnPointsCommand request, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(request.CustomerId);
        var maybe = await customerQueries.GetById(customerId, cancellationToken);

        return await maybe.Match<Task<Result<Customer, CustomerException>>>(
            async customer =>
            {
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
                catch (Exception ex)
                {
                    return new CustomerUnknownException(customerId, ex);
                }
            },
            () => Task.FromResult<Result<Customer, CustomerException>>(
                new CustomerNotFoundException(customerId)));
    }
}
