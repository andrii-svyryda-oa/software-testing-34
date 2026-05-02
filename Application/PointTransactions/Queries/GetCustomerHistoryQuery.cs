using Application.Common;
using Application.Common.Interfaces.Queries;
using Application.Customers.Exceptions;
using Domain.Customers;
using Domain.PointTransactions;
using MediatR;

namespace Application.PointTransactions.Queries;

public record GetCustomerHistoryQuery(Guid CustomerId, int Skip, int Take)
    : IRequest<Result<PaginatedResult<PointTransaction>, CustomerException>>;

public class GetCustomerHistoryQueryHandler(
    ICustomerQueries customerQueries,
    IPointTransactionQueries transactionQueries)
    : IRequestHandler<GetCustomerHistoryQuery, Result<PaginatedResult<PointTransaction>, CustomerException>>
{
    public async Task<Result<PaginatedResult<PointTransaction>, CustomerException>> Handle(
        GetCustomerHistoryQuery request, CancellationToken cancellationToken)
    {
        var customerId = new CustomerId(request.CustomerId);
        var maybe = await customerQueries.GetById(customerId, cancellationToken);
        if (!maybe.HasValue)
            return new CustomerNotFoundException(customerId);

        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 100);

        var dataTask = transactionQueries.GetHistoryFor(customerId, skip, take, cancellationToken);
        var countTask = transactionQueries.CountFor(customerId, cancellationToken);
        await Task.WhenAll(dataTask, countTask);

        return new PaginatedResult<PointTransaction>(dataTask.Result, countTask.Result);
    }
}
