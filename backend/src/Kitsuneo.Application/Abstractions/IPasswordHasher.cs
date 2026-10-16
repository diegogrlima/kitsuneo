namespace Kitsuneo.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string passwordHash, string password);
}

public enum PasswordCheck
{
    Failed = 0,
    Success = 1,

    /// <summary>Correct, but hashed with older settings; it should be hashed again and saved.</summary>
    SuccessRehashNeeded = 2
}
