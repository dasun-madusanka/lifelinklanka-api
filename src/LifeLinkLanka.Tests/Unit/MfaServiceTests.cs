using FluentAssertions;
using LifeLinkLanka.Infrastructure.Identity;
using Xunit;

namespace LifeLinkLanka.Tests.Unit;

public class MfaServiceTests
{
    private readonly MfaService _sut = new();

    [Fact]
    public void GenerateSecretKey_ReturnsNonEmptyBase32String()
    {
        var secret = _sut.GenerateSecretKey();
        secret.Should().NotBeNullOrWhiteSpace();
        secret.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public void ValidateCode_WithWrongCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecretKey();
        var result = _sut.ValidateCode(secret, "000000");
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateQrCodeUri_ContainsIssuerAndSecret()
    {
        var secret = _sut.GenerateSecretKey();
        var uri = _sut.GenerateQrCodeUri("donor@example.com", secret);

        uri.Should().Contain("otpauth://totp/");
        uri.Should().Contain(secret);
        uri.Should().Contain("LifeLinkLanka");
    }
}