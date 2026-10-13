using Kitsuneo.Application.Abstractions;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;

namespace Kitsuneo.Application.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenBlacklist tokenBlacklist,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    RefreshTokenSettings refreshTokenSettings,
    TimeProvider timeProvider)
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "Invalid or expired session. Log in again.";

    // Verified against when the email is unknown, so both cases take the same time
    private static string? _unknownUserHash;

    /// <exception cref="DomainConflictException">The email is already registered.</exception>
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = User.NormalizeEmail(request.Email);
        PasswordPolicy.Validate(request.Password);

        if (await userRepository.EmailExistsAsync(email, cancellationToken))
            throw new DomainConflictException("Email is already registered.");

        var user = User.Create(email, passwordHasher.Hash(request.Password));
        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return await StartSessionAsync(user, cancellationToken);
    }

    /// <exception cref="AuthenticationFailedException">Unknown email or wrong password; the message doesn't say which.</exception>
    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        User? user = null;
        try
        {
            user = await userRepository.GetByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        }
        catch (DomainException)
        {
            // A malformed email can't belong to anyone; answer like any other wrong login
        }

        var password = request.Password ?? string.Empty;
        if (user is null)
        {
            passwordHasher.Verify(_unknownUserHash ??= passwordHasher.Hash("unknown-user"), password);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        var check = passwordHasher.Verify(user.PasswordHash, password);
        if (check == PasswordCheck.Failed)
            throw new AuthenticationFailedException(InvalidCredentials);

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(passwordHasher.Hash(password));
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        return await StartSessionAsync(user, cancellationToken);
    }

    /// <summary>
    /// Trades a refresh token for a new access token and a new refresh token (rotation).
    /// Reusing a token that was already traded revokes the whole session, since it means the token leaked.
    /// </summary>
    /// <exception cref="AuthenticationFailedException">The token is unknown, expired, revoked or reused.</exception>
    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw new AuthenticationFailedException(InvalidRefreshToken);

        var now = Now();
        var current = await refreshTokenRepository.GetByHashAsync(RefreshTokenSecret.Hash(refreshToken), cancellationToken);
        if (current is null)
            throw new AuthenticationFailedException(InvalidRefreshToken);

        if (current.RevokedAt is not null)
        {
            await refreshTokenRepository.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw new AuthenticationFailedException(InvalidRefreshToken);
        }

        if (!current.IsActive(now))
            throw new AuthenticationFailedException(InvalidRefreshToken);

        var user = await userRepository.GetByIdAsync(current.UserId, cancellationToken)
            ?? throw new AuthenticationFailedException(InvalidRefreshToken);

        var (rawToken, next) = NewRefreshToken(user.Id, now, current.FamilyId);

        // Two requests with the same token: only one wins; the loser is treated as a reuse
        if (!await refreshTokenRepository.TryRotateAsync(current.Id, next.Id, now, cancellationToken))
        {
            await refreshTokenRepository.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            throw new AuthenticationFailedException(InvalidRefreshToken);
        }

        await refreshTokenRepository.AddAsync(next, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result(user, accessTokenIssuer.Issue(user, now), rawToken, next);
    }

    /// <summary>
    /// Ends the session: revokes the refresh token's whole session (if it belongs to the user) and
    /// blacklists the access token used for this request until it expires.
    /// </summary>
    public async Task LogoutAsync(
        Guid userId,
        string jti,
        DateTime accessTokenExpiresAt,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = Now();

        if (!string.IsNullOrEmpty(refreshToken))
        {
            var token = await refreshTokenRepository.GetByHashAsync(RefreshTokenSecret.Hash(refreshToken), cancellationToken);
            if (token is not null && token.UserId == userId)
                await refreshTokenRepository.RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
        }

        await tokenBlacklist.RevokeAsync(jti, userId, accessTokenExpiresAt, cancellationToken);
    }

    /// <summary>Returns null when the user no longer exists.</summary>
    public async Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : UserResponse.FromEntity(user);
    }

    private async Task<AuthResult> StartSessionAsync(User user, CancellationToken cancellationToken)
    {
        var now = Now();
        var (rawToken, refreshToken) = NewRefreshToken(user.Id, now, familyId: null);

        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result(user, accessTokenIssuer.Issue(user, now), rawToken, refreshToken);
    }

    private (string RawToken, RefreshToken Entity) NewRefreshToken(Guid userId, DateTime now, Guid? familyId)
    {
        var rawToken = RefreshTokenSecret.Generate();
        var entity = RefreshToken.Issue(
            userId, RefreshTokenSecret.Hash(rawToken), now, refreshTokenSettings.Lifetime, familyId);

        return (rawToken, entity);
    }

    private static AuthResult Result(User user, AccessToken accessToken, string rawRefreshToken, RefreshToken refreshToken) =>
        new(
            UserResponse.FromEntity(user),
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
