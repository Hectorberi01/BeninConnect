using System.Security.Claims;
using BeninConnect.Modules.Identity.Application;
using BeninConnect.Modules.Identity.Domain;
using BeninConnect.Shared.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BeninConnect.Modules.Identity.Infrastructure;

public sealed class MemberTokenService(JwtOptions options, TimeProvider clock) : IMemberTokenService
{
    public string CreateToken(User user) => options.CreateToken(options.Audience, clock.GetUtcNow().UtcDateTime, TimeSpan.FromDays(options.LifetimeDays),
    [
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Name, user.DisplayName)
    ]);
}
