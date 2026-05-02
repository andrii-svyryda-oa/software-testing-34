using FluentValidation;

namespace Application.Customers.Commands;

public class EarnPointsCommandValidator : AbstractValidator<EarnPointsCommand>
{
    public EarnPointsCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.BasePoints).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
