using System.Security.Cryptography;
using LifeLinkLanka.Application.Interfaces;
using OtpNet;
using QRCoder;

namespace LifeLinkLanka.Infrastructure.Identity;

public class MfaService : IMfaService
{
    public string GenerateSecretKey()
    {
        var bytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(bytes);
    }

    public string GenerateQrCodeUri(string email, string secretKey, string issuer = "LifeLinkLanka")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
               $"?secret={secretKey}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";
    }

    public byte[] GenerateQrCodePng(string qrUri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrUri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(10);
    }

    public bool ValidateCode(string secretKey, string userInputCode)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secretKey));
        return totp.VerifyTotp(userInputCode, out _, new VerificationWindow(1, 1));
    }
}