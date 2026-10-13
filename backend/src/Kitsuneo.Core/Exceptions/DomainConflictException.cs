namespace Kitsuneo.Core.Exceptions;

/// <summary>The request is valid, but conflicts with the current state (e.g. doing something that can only be done once).</summary>
public sealed class DomainConflictException(string message) : DomainException(message)
{

}
