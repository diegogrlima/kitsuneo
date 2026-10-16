using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Auth;

public sealed record UserResponse(Guid Id, string Email, DateTime CreatedAt)
{
    public static UserResponse FromEntity(User user) => new(user.Id, user.Email, user.CreatedAt);
}
