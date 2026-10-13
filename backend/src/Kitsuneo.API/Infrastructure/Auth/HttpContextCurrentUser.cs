using Kitsuneo.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kitsuneo.API.Infrastructure.Auth;

/// <summary>Reads the user id from the validated access token's "sub" claim.</summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid Id =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("The request is not authenticated.");
}
