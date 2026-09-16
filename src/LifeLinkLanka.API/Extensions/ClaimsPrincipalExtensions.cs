using System.Security.Claims;

namespace LifeLinkLanka.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var rawId = principal.FindFirstValue("sub")
                 ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? principal.FindFirst("sub")?.Value
                 ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(rawId) || !Guid.TryParse(rawId, out var userId))
        {
            return Guid.Empty;
        }

        return userId;
    }
}
