namespace Kitsuneo.Application.Abstractions;

/// <summary>The authenticated user of the current request.</summary>
public interface ICurrentUser
{
    /// <exception cref="InvalidOperationException">The request is not authenticated.</exception>
    Guid Id { get; }
}
