using Api.Dtos;
using FluentValidation;

namespace Api.Modules.Validators;

public class CreateRewardDtoValidator : AbstractValidator<CreateRewardDto>
{
    public CreateRewardDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(3, 255);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.PointsCost).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
    }
}
