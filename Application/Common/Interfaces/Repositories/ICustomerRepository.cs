using Domain.Customers;
using Optional;

namespace Application.Common.Interfaces.Repositories;

/// <summary>
/// Write-side repository for <see cref="Customer"/>. <c>Add</c> and <c>Update</c> stage
/// changes only; callers must invoke <see cref="Application.Common.Interfaces.IUnitOfWork.SaveChangesAsync"/>
/// to persist them, enabling atomic multi-aggregate writes.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer> Add(Customer customer, CancellationToken cancellationToken);
    Customer Update(Customer customer);
    Task<Option<Customer>> GetById(CustomerId id, CancellationToken cancellationToken);
    Task<Option<Customer>> GetByEmail(string email, CancellationToken cancellationToken);
}
