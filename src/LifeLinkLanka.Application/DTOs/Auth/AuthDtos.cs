namespace LifeLinkLanka.Application.DTOs.Auth;

public record RegisterDto(string FullName, string Email, string Password, string NicNumber, string District, DateTime DateOfBirth, string Role);
public record LoginDto(string Email, string Password);
public record LoginResultDto(bool RequiresMfa, string? MfaChallengeToken, TokenResponseDto? Tokens);
public record MfaSetupResponseDto(string SecretKey, string QrCodeBase64);
public record MfaVerifyDto(string MfaChallengeToken, string Code);
public record TokenResponseDto(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken);
public record RefreshTokenDto(string AccessToken, string RefreshToken);