using Application.Common;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Repositories;
using Application.Customers.Exceptions;
using Domain.Customers;
using MediatR;

namespace Application.Customers.Commands;

public record RegisterCustomerCommand : IRequest<Result<Customer, CustomerException>>
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}

public class RegisterCustomerCommandHandler(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterCustomerCommand, Result<Customer, CustomerException>>
{
    public async Task<Result<Customer, CustomerException>> Handle(
        RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var existing = await customerRepository.GetByEmail(request.Email, cancellationToken);
        return await existing.Match<Task<Result<Customer, CustomerException>>>(
            c => Task.FromResult<Result<Customer, CustomerException>>(
                new CustomerAlreadyExistsException(c.Id)),
            async () =>
            {
                try
                {
                    var customer = Customer.Register(
                        CustomerId.New(),
                        request.Name,
                        request.Email,
                        request.Phone,
                        DateTime.UtcNow);

                    await customerRepository.Add(customer, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return customer;
                }
                catch (Exception ex)
                {
                    return new CustomerUnknownException(CustomerId.Empty(), ex);
                }
            });
    }
}
