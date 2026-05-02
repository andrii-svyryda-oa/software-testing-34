using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Optional;

namespace Infrastructure.Persistence.Repositories;

public class CustomerRepository(ApplicationDbContext context) : ICustomerRepository, ICustomerQueries
{
    Task<Option<Customer>> ICustomerRepository.GetById(CustomerId id, CancellationToken cancellationToken)
        => GetByIdTracked(id, cancellationToken);

    Task<Option<Customer>> ICustomerQueries.GetById(CustomerId id, CancellationToken cancellationToken)
        => GetByIdAsNoTracking(id, cancellationToken);

    Task<Option<Customer>> ICustomerRepository.GetByEmail(string email, CancellationToken cancellationToken)
        => GetByEmail(email, asNoTracking: true, cancellationToken);

    Task<Option<Customer>> ICustomerQueries.GetByEmail(string email, CancellationToken cancellationToken)
        => GetByEmail(email, asNoTracking: true, cancellationToken);

    private async Task<Option<Customer>> GetByIdTracked(CustomerId id, CancellationToken ct)
    {
        var entity = await context.Customers.FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? Option.None<Customer>() : Option.Some(entity);
    }

    private async Task<Option<Customer>> GetByIdAsNoTracking(CustomerId id, CancellationToken ct)
    {
        var entity = await context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? Option.None<Customer>() : Option.Some(entity);
    }

    private async Task<Option<Customer>> GetByEmail(string email, bool asNoTracking, CancellationToken ct)
    {
        var query = context.Customers.AsQueryable();
        if (asNoTracking) query = query.AsNoTracking();
        var entity = await query.FirstOrDefaultAsync(x => x.Email == email, ct);
        return entity is null ? Option.None<Customer>() : Option.Some(entity);
    }

    public async Task<Customer> Add(Customer customer, CancellationToken cancellationToken)
    {
        await context.Customers.AddAsync(customer, cancellationToken);
        return customer;
    }

    public Customer Update(Customer customer)
    {
        var entry = context.Entry(customer);
        if (entry.State == EntityState.Detached)
        {
            context.Customers.Update(customer);
        }
        return customer;
    }
}
