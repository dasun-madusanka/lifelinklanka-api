namespace LifeLinkLanka.Application.Interfaces;

public interface IMfaService
{
    string GenerateSecretKey();
    string GenerateQrCodeUri(string email, string secretKey, string issuer = "LifeLinkLanka");
    byte[] GenerateQrCodePng(string qrUri);
    bool ValidateCode(string secretKey, string userInputCode);
}