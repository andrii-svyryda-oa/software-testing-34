using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators;

public class RegisterCustomerDtoValidator : AbstractValidator<RegisterCustomerDto>
{
    public RegisterCustomerDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(3, 255);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Phone)
            .NotEmpty()
            .Matches(@"^\+?[0-9\s\-()]{7,20}$");
    }
}

public class EarnPointsDtoValidator : AbstractValidator<EarnPointsDto>
{
    public EarnPointsDtoValidator()
    {
        RuleFor(x => x.BasePoints).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class RedeemPointsDtoValidator : AbstractValidator<RedeemPointsDto>
{
    public RedeemPointsDtoValidator()
    {
        RuleFor(x => x.RewardId).NotEmpty();
    }
}
