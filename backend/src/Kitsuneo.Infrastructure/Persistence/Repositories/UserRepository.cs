using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kitsuneo.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await context.Users.AddAsync(user, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        // Two sign-ups with the same email at once: the unique index stops the second
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainConflictException("Email is already registered.");
        }
    }
}
