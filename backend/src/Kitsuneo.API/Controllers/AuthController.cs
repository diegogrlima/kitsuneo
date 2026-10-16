using Kitsuneo.API.Infrastructure.Auth;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Kitsuneo.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    public const string RateLimitPolicy = "auth";

    /// <summary>Creates an account and logs in. The refresh token is set as an HttpOnly cookie.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);

        RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
        return CreatedAtAction(nameof(Me), AuthResponse.From(result));
    }

    /// <summary>Logs in with email and password. The refresh token is set as an HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);

        RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(AuthResponse.From(result));
    }

    /// <summary>Trades the refresh token cookie for a new access token, rotating the cookie too.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RefreshAsync(RefreshTokenCookie.Read(Request), cancellationToken);

            RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
            return Ok(AuthResponse.From(result));
        }
        catch (AuthenticationFailedException)
        {
            // A dead cookie is useless; drop it so the client stops sending it
            RefreshTokenCookie.Delete(Response);
            throw;
        }
    }

    /// <summary>Ends the session: revokes the refresh token and blacklists the current access token until it expires.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)!.Value;
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(User.FindFirst(JwtRegisteredClaimNames.Exp)!.Value)).UtcDateTime;

        await authService.LogoutAsync(currentUser.Id, jti, expiresAt, RefreshTokenCookie.Read(Request), cancellationToken);

        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await authService.GetUserAsync(currentUser.Id, cancellationToken);

        // The token outlived its user (account deleted): the caller must log in again
        return user is null ? Unauthorized() : Ok(user);
    }
}
