using FluentValidation;

namespace Application.Customers.Commands;

public class RedeemPointsCommandValidator : AbstractValidator<RedeemPointsCommand>
{
    public RedeemPointsCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.RewardId).NotEmpty();
    }
}
