namespace Kitsuneo.Application.Auth;

/// <summary>Credentials or tokens were not accepted. The message is safe to show and never says which part was wrong.</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message)
{

}
