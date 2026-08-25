using LifeLinkLanka.Domain.Entities;

namespace LifeLinkLanka.Application.Interfaces;

public interface IJwtService
{
    (string accessToken, DateTime expiresAtUtc) GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
}