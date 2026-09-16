namespace LifeLinkLanka.Application.DTOs.Auth;

public record RegisterDto(
    string FullName,
    string Email,
    string Password,
    string NicNumber,
    string District,
    DateTime DateOfBirth,
    string Role,
    string? PhoneNumber = null,
    string? BloodType = null,
    double? WeightKg = null,
    string? HospitalName = null,
    string? HospitalRegistrationNumber = null,
    string? HospitalDistrict = null,
    string? HospitalAddress = null,
    string? HospitalContactPhone = null,
    string? BloodBankName = null,
    string? BloodBankDistrict = null,
    string? BloodBankAddress = null,
    string? BloodBankContactPhone = null,
    string? VerificationDocumentUrl = null,
    string? VerificationDocumentName = null,
    string? VerificationDocumentType = null);
public record LoginDto(string Email, string Password);
public record LoginResultDto(bool RequiresMfa, string? MfaChallengeToken, TokenResponseDto? Tokens);
public record MfaSetupResponseDto(string SecretKey, string QrCodeBase64);
public record MfaVerifyDto(string MfaChallengeToken, string Code);
public record TokenResponseDto(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken);
public record RefreshTokenDto(string AccessToken, string RefreshToken);
public record RejectUserDto(string? Reason);
public record PendingUserApprovalDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string NicNumber,
    string District,
    DateTime DateOfBirth,
    string Role,
    string AccountStatus,
    DateTime CreatedAtUtc,
    string? VerificationDocumentUrl,
    string? VerificationDocumentName,
    string? VerificationDocumentType,
    string? FacilityName,
    string? BloodType,
    double? WeightKg);