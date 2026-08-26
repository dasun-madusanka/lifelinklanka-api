using LifeLinkLanka.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace LifeLinkLanka.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = default!;
    public string NicNumber { get; set; } = default!;         // Sri Lankan NIC
    public string District { get; set; } = default!;          // e.g. "Colombo", "Kandy"
    public DateTime DateOfBirth { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // MFA
    public bool IsMfaEnabled { get; set; } = false;
    public string? MfaSecretKey { get; set; }                 // encrypted at rest
    public DateTime? MfaEnabledAtUtc { get; set; }

    // Refresh token
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryUtc { get; set; }

    public VerificationStatus AccountStatus { get; set; } = VerificationStatus.Pending;
    public bool IsActive { get; set; } = true;

    public DonorProfile? DonorProfile { get; set; }
}