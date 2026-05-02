using FluentValidation;

namespace Application.Customers.Commands;

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(3, 255);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Matches(@"^\+?[0-9\s\-()]{7,20}$");
    }
}
