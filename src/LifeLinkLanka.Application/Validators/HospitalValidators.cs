using FluentValidation;
using LifeLinkLanka.Application.DTOs.Hospital;

namespace LifeLinkLanka.Application.Validators;

public class CreateHospitalDtoValidator : AbstractValidator<CreateHospitalDto>
{
    public CreateHospitalDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RegistrationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.District).NotEmpty();
        RuleFor(x => x.Address).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ContactPhone).NotEmpty().Matches(@"^(0|\+94)[0-9]{9}$")
            .WithMessage("Contact phone must be a valid Sri Lankan number (e.g. 0771234567 or +94771234567).");
    }
}