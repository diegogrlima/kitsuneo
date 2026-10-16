using FluentAssertions;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Auth;
using Kitsuneo.Core.Entities;
using Kitsuneo.Core.Exceptions;
using NSubstitute;

namespace Kitsuneo.Application.Tests.Auth;

public class AuthServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenBlacklist _blacklist = Substitute.For<ITokenBlacklist>();
    private readonly IAccessTokenIssuer _issuer = Substitute.For<IAccessTokenIssuer>();
    private readonly List<RefreshToken> _savedRefreshTokens = [];
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _issuer.Issue(Arg.Any<User>(), Arg.Any<DateTime>())
            .Returns(call => new AccessToken($"jwt-for-{call.Arg<User>().Email}", "jti-1", call.Arg<DateTime>().AddMinutes(15)));
        _refreshTokens.AddAsync(Arg.Do<RefreshToken>(_savedRefreshTokens.Add), Arg.Any<CancellationToken>());
        _refreshTokens.TryRotateAsync(default, default, default, default).ReturnsForAnyArgs(true);

        _service = new AuthService(
            _users,
            _refreshTokens,
            _blacklist,
            new FakePasswordHasher(),
            _issuer,
            new RefreshTokenSettings(RefreshLifetime),
            new FixedTimeProvider(Now));
    }

    // RegisterAsync

    [Fact]
    public async Task RegisterAsync_CreatesUserWithHashedPasswordAndStartsSession()
    {
        User? saved = null;
        await _users.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>());

        var result = await _service.RegisterAsync(new RegisterRequest(" Ana@Example.com ", "correct horse"));

        saved!.Email.Should().Be("ana@example.com");
        saved.PasswordHash.Should().Be("hashed:correct horse");
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        result.User.Should().Be(UserResponse.FromEntity(saved));
        result.AccessToken.Should().Be("jwt-for-ana@example.com");
        result.AccessTokenExpiresAt.Should().Be(Now.AddMinutes(15));
        result.RefreshTokenExpiresAt.Should().Be(Now + RefreshLifetime);

        // Only the hash is stored, and it matches the raw token handed out
        var stored = _savedRefreshTokens.Should().ContainSingle().Subject;
        stored.UserId.Should().Be(saved.Id);
        stored.TokenHash.Should().Be(RefreshTokenSecret.Hash(result.RefreshToken)).And.NotBe(result.RefreshToken);
    }

    [Fact]
    public async Task RegisterAsync_WithTakenEmail_ThrowsConflictAndSavesNothing()
    {
        _users.EmailExistsAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _service.RegisterAsync(new RegisterRequest("ANA@example.com", "correct horse"));

        await act.Should().ThrowAsync<DomainConflictException>().WithMessage("Email is already registered.");
        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Theory]
    [InlineData("short", "Password must have at least 8 characters.")]
    [InlineData("        ", "Password cannot be only spaces.")]
    public async Task RegisterAsync_WithWeakPassword_ThrowsDomainException(string password, string message)
    {
        var act = () => _service.RegisterAsync(new RegisterRequest("ana@example.com", password));

        await act.Should().ThrowAsync<DomainException>().WithMessage(message);
        await _users.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordOver128Chars_ThrowsDomainException()
    {
        var act = () => _service.RegisterAsync(new RegisterRequest("ana@example.com", new string('a', 129)));

        await act.Should().ThrowAsync<DomainException>().WithMessage("Password must have at most 128 characters.");
    }

    // LoginAsync

    [Fact]
    public async Task LoginAsync_WithRightPassword_StartsSession()
    {
        var user = ArrangeUser("ana@example.com", "correct horse");

        var result = await _service.LoginAsync(new LoginRequest("ANA@example.com", "correct horse"));

        result.User.Id.Should().Be(user.Id);
        result.AccessToken.Should().Be("jwt-for-ana@example.com");
        _savedRefreshTokens.Should().ContainSingle(t => t.UserId == user.Id);
    }

    [Theory]
    [InlineData("ana@example.com", "wrong password")]
    [InlineData("nobody@example.com", "correct horse")]
    [InlineData("not an email", "correct horse")]
    public async Task LoginAsync_WithBadCredentials_FailsWithSameMessage(string email, string password)
    {
        ArrangeUser("ana@example.com", "correct horse");

        var act = () => _service.LoginAsync(new LoginRequest(email, password));

        await act.Should().ThrowAsync<AuthenticationFailedException>().WithMessage("Invalid email or password.");
        _savedRefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_WithOutdatedHash_RehashesAndSaves()
    {
        var user = ArrangeUser("ana@example.com", "correct horse", hashPrefix: "old:");

        await _service.LoginAsync(new LoginRequest("ana@example.com", "correct horse"));

        user.PasswordHash.Should().Be("hashed:correct horse");
        await _users.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // RefreshAsync

    [Fact]
    public async Task RefreshAsync_WithActiveToken_RotatesWithinSameSession()
    {
        var user = ArrangeUser("ana@example.com", "correct horse");
        var (raw, current) = ArrangeRefreshToken(user.Id, issuedAt: Now.AddDays(-1));

        var result = await _service.RefreshAsync(raw);

        var next = _savedRefreshTokens.Should().ContainSingle().Subject;
        next.FamilyId.Should().Be(current.FamilyId);
        next.TokenHash.Should().Be(RefreshTokenSecret.Hash(result.RefreshToken));
        result.RefreshToken.Should().NotBe(raw);
        result.AccessToken.Should().Be("jwt-for-ana@example.com");
        await _refreshTokens.Received(1).TryRotateAsync(current.Id, next.Id, Now, Arg.Any<CancellationToken>());
        await _refreshTokens.DidNotReceiveWithAnyArgs().RevokeFamilyAsync(default, default, default);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-token")]
    public async Task RefreshAsync_WithMissingOrUnknownToken_Fails(string? raw)
    {
        var act = () => _service.RefreshAsync(raw);

        await act.Should().ThrowAsync<AuthenticationFailedException>().WithMessage("Invalid or expired session. Log in again.");
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_FailsWithoutRotating()
    {
        var user = ArrangeUser("ana@example.com", "correct horse");
        var (raw, _) = ArrangeRefreshToken(user.Id, issuedAt: Now - RefreshLifetime);

        var act = () => _service.RefreshAsync(raw);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        await _refreshTokens.DidNotReceiveWithAnyArgs().TryRotateAsync(default, default, default, default);
    }

    [Fact]
    public async Task RefreshAsync_WithAlreadyUsedToken_RevokesWholeSession()
    {
        var user = ArrangeUser("ana@example.com", "correct horse");
        var (raw, used) = ArrangeRefreshToken(user.Id, issuedAt: Now.AddDays(-1), revoked: true);

        var act = () => _service.RefreshAsync(raw);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        await _refreshTokens.Received(1).RevokeFamilyAsync(used.FamilyId, Now, Arg.Any<CancellationToken>());
        _savedRefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_WhenConcurrentRefreshWon_RevokesWholeSession()
    {
        var user = ArrangeUser("ana@example.com", "correct horse");
        var (raw, current) = ArrangeRefreshToken(user.Id, issuedAt: Now.AddDays(-1));
        _refreshTokens.TryRotateAsync(default, default, default, default).ReturnsForAnyArgs(false);

        var act = () => _service.RefreshAsync(raw);

        await act.Should().ThrowAsync<AuthenticationFailedException>();
        await _refreshTokens.Received(1).RevokeFamilyAsync(current.FamilyId, Now, Arg.Any<CancellationToken>());
        _savedRefreshTokens.Should().BeEmpty();
    }

    // LogoutAsync

    [Fact]
    public async Task LogoutAsync_RevokesSessionAndBlacklistsAccessToken()
    {
        var userId = Guid.NewGuid();
        var (raw, token) = ArrangeRefreshToken(userId, issuedAt: Now.AddHours(-1));

        await _service.LogoutAsync(userId, "jti-1", Now.AddMinutes(10), raw);

        await _refreshTokens.Received(1).RevokeFamilyAsync(token.FamilyId, Now, Arg.Any<CancellationToken>());
        await _blacklist.Received(1).RevokeAsync("jti-1", userId, Now.AddMinutes(10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WithOtherUsersRefreshToken_OnlyBlacklistsAccessToken()
    {
        var (raw, _) = ArrangeRefreshToken(Guid.NewGuid(), issuedAt: Now.AddHours(-1));
        var userId = Guid.NewGuid();

        await _service.LogoutAsync(userId, "jti-1", Now.AddMinutes(10), raw);

        await _refreshTokens.DidNotReceiveWithAnyArgs().RevokeFamilyAsync(default, default, default);
        await _blacklist.Received(1).RevokeAsync("jti-1", userId, Now.AddMinutes(10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WithoutRefreshToken_StillBlacklistsAccessToken()
    {
        var userId = Guid.NewGuid();

        await _service.LogoutAsync(userId, "jti-1", Now.AddMinutes(10), refreshToken: null);

        await _blacklist.Received(1).RevokeAsync("jti-1", userId, Now.AddMinutes(10), Arg.Any<CancellationToken>());
    }

    private User ArrangeUser(string email, string password, string hashPrefix = "hashed:")
    {
        var user = User.Create(email, hashPrefix + password);
        _users.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private (string Raw, RefreshToken Token) ArrangeRefreshToken(Guid userId, DateTime issuedAt, bool revoked = false)
    {
        var raw = RefreshTokenSecret.Generate();
        var token = RefreshToken.Issue(userId, RefreshTokenSecret.Hash(raw), issuedAt, RefreshLifetime);
        if (revoked)
            typeof(RefreshToken).GetProperty(nameof(RefreshToken.RevokedAt))!.SetValue(token, issuedAt.AddMinutes(1));

        _refreshTokens.GetByHashAsync(token.TokenHash, Arg.Any<CancellationToken>()).Returns(token);
        return (raw, token);
    }

    // "hashed:" is current, "old:" needs a rehash; anything else fails
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public PasswordCheck Verify(string passwordHash, string password) =>
            passwordHash == "hashed:" + password ? PasswordCheck.Success
            : passwordHash == "old:" + password ? PasswordCheck.SuccessRehashNeeded
            : PasswordCheck.Failed;
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
