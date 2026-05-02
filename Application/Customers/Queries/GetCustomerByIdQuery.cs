using Application.Common.Interfaces.Queries;
using Domain.Customers;
using MediatR;
using Optional;

namespace Application.Customers.Queries;

public record GetCustomerByIdQuery(Guid CustomerId) : IRequest<Option<Customer>>;

public class GetCustomerByIdQueryHandler(ICustomerQueries customerQueries)
    : IRequestHandler<GetCustomerByIdQuery, Option<Customer>>
{
    public Task<Option<Customer>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
        => customerQueries.GetById(new CustomerId(request.CustomerId), cancellationToken);
}
