using Kitsuneo.Application.Auth;
using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Abstractions;

public interface IAccessTokenIssuer
{
    /// <summary>A signed, short-lived JWT for <paramref name="user"/>, with a unique id (jti).</summary>
    AccessToken Issue(User user, DateTime now);
}
