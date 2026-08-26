using FluentValidation;
using LifeLinkLanka.Application.DTOs.Auth;

namespace LifeLinkLanka.Application.Validators;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);

        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        // Sri Lankan NIC: old format (9 digits + V/X) or new format (12 digits)
        RuleFor(x => x.NicNumber)
            .NotEmpty()
            .Matches(@"^([0-9]{9}[vVxX]|[0-9]{12})$")
            .WithMessage("NIC must be in old format (9 digits + V/X) or new format (12 digits).");

        RuleFor(x => x.District).NotEmpty();

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.UtcNow.AddYears(-18))
            .WithMessage("Donor must be at least 18 years old to register.");
    }
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class MfaVerifyDtoValidator : AbstractValidator<MfaVerifyDto>
{
    public MfaVerifyDtoValidator()
    {
        RuleFor(x => x.MfaChallengeToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches(@"^\d{6}$")
            .WithMessage("MFA code must be exactly 6 digits.");
    }
}