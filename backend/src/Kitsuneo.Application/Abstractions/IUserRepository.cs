using Kitsuneo.Core.Entities;

namespace Kitsuneo.Application.Abstractions;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>The user tracked for changes; <paramref name="normalizedEmail"/> must come from <see cref="User.NormalizeEmail"/>.</summary>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <exception cref="Core.Exceptions.DomainConflictException">Another user took the email in the meantime.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
