using FluentValidation;

namespace Application.PointTransactions.Commands;

public class ExpirePointsCommandValidator : AbstractValidator<ExpirePointsCommand>
{
    public ExpirePointsCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.At)
            .GreaterThan(default(DateTime))
            .LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1));
    }
}
