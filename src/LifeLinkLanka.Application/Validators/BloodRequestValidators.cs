using FluentValidation;
using LifeLinkLanka.Application.DTOs.BloodRequest;

namespace LifeLinkLanka.Application.Validators;

public class CreateBloodRequestDtoValidator : AbstractValidator<CreateBloodRequestDto>
{
    public CreateBloodRequestDtoValidator()
    {
        RuleFor(x => x.HospitalId).NotEmpty();
        RuleFor(x => x.UnitsNeeded).GreaterThan(0).LessThanOrEqualTo(50);
        RuleFor(x => x.PatientContext).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NeededByUtc).GreaterThan(DateTime.UtcNow)
            .WithMessage("NeededByUtc must be a future date/time.");
    }
}