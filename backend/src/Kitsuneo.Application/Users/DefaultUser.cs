namespace Kitsuneo.Application.Users;

/// <summary>
/// Fixed user that owns every phrase while there is no login.
/// The foreign key to the users table comes once that table exists.
/// </summary>
public static class DefaultUser
{
    public static readonly Guid Id = new("0199d8a4-7c3e-7000-8000-000000000001");
}
