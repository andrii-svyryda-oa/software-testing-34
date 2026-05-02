using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Optional;

namespace Infrastructure.Persistence.Repositories;

public class CustomerRepository(ApplicationDbContext context) : ICustomerRepository, ICustomerQueries
{
    public async Task<Option<Customer>> GetById(CustomerId id, CancellationToken cancellationToken)
    {
        var entity = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? Option.None<Customer>() : Option.Some(entity);
    }

    public async Task<Option<Customer>> GetByEmail(string email, CancellationToken cancellationToken)
    {
        var entity = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        return entity is null ? Option.None<Customer>() : Option.Some(entity);
    }

    public async Task<Customer> Add(Customer customer, CancellationToken cancellationToken)
    {
        await context.Customers.AddAsync(customer, cancellationToken);
        return customer;
    }

    public Customer Update(Customer customer)
    {
        context.Customers.Update(customer);
        return customer;
    }
}
