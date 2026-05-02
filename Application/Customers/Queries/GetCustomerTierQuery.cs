using Application.Common;
using Application.Common.Interfaces.Queries;
using Application.Customers.Exceptions;
using Domain.Customers;
using MediatR;

namespace Application.Customers.Queries;

public record CustomerTierResult
{
    public required TierLevel Current { get; init; }
    public required TierLevel Next { get; init; }
    public required int TotalEarnedPoints { get; init; }
    public required int PointsToNext { get; init; }
}

public record GetCustomerTierQuery(Guid CustomerId)
    : IRequest<Result<CustomerTierResult, CustomerException>>;

public class GetCustomerTierQueryHandler(ICustomerQueries customerQueries)
    : IRequestHandler<GetCustomerTierQuery, Result<CustomerTierResult, CustomerException>>
{
    public async Task<Result<CustomerTierResult, CustomerException>> Handle(
        GetCustomerTierQuery request, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(request.CustomerId);
        var maybe = await customerQueries.GetById(customerId, cancellationToken);

        return maybe.Match<Result<CustomerTierResult, CustomerException>>(
            customer =>
            {
                var (next, pointsToNext) = TierLevels.ProgressFrom(customer.TotalEarnedPoints);
                return new CustomerTierResult
                {
                    Current = customer.TierLevel,
                    Next = next,
                    TotalEarnedPoints = customer.TotalEarnedPoints,
                    PointsToNext = pointsToNext
                };
            },
            () => new CustomerNotFoundException(customerId));
    }
}
