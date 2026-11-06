using System.Security.Claims;
using BeninConnect.Shared.Application;

namespace BeninConnect.Shared.Api;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid Id =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? accessor.HttpContext?.User.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Authentification requise.");
}
