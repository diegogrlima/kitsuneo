using Kitsuneo.Application.Abstractions;

namespace Kitsuneo.Application.Tests;

/// <summary>Stands in for the authenticated user of the request.</summary>
internal sealed class FakeCurrentUser(Guid id) : ICurrentUser
{
    public Guid Id => id;
}
