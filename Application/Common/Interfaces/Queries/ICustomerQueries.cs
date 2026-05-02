using Domain.Customers;
using Optional;

namespace Application.Common.Interfaces.Queries;

public interface ICustomerQueries
{
    Task<Option<Customer>> GetById(CustomerId id, CancellationToken cancellationToken);
    Task<Option<Customer>> GetByEmail(string email, CancellationToken cancellationToken);
}
