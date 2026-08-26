using FluentValidation;
using LifeLinkLanka.Application.DTOs.Donor;

namespace LifeLinkLanka.Application.Validators;

public class UpsertDonorProfileDtoValidator : AbstractValidator<UpsertDonorProfileDto>
{
    public UpsertDonorProfileDtoValidator()
    {
        RuleFor(x => x.WeightKg).GreaterThanOrEqualTo(45)
            .WithMessage("Minimum donor weight is 45kg per National Blood Transfusion Service guidelines.")
            .LessThanOrEqualTo(200);
    }
}