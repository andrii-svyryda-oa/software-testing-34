using Domain.Customers;
using Optional;

namespace Application.Common.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<Customer> Add(Customer customer, CancellationToken cancellationToken);
    Task<Customer> Update(Customer customer, CancellationToken cancellationToken);
    Task<Option<Customer>> GetById(CustomerId id, CancellationToken cancellationToken);
    Task<Option<Customer>> GetByEmail(string email, CancellationToken cancellationToken);
}
