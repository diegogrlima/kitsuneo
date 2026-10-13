using System.Threading.RateLimiting;
using Kitsuneo.API.Controllers;
using Kitsuneo.Application.Abstractions;
using Kitsuneo.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kitsuneo.API.Infrastructure.Auth;

public static class AuthenticationSetup
{
    /// <summary>
    /// JWT bearer authentication, with every endpoint requiring a user unless marked [AllowAnonymous],
    /// the blacklist checked on each request, and rate limiting for the auth endpoints.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured from JwtOptions, which Infrastructure binds and validates at startup
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names as issued ("sub", "jti") instead of the long .NET claim types
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtRegisteredClaimNames.Email,
                    // The default 5 minutes would keep a 15-minute token alive a third longer
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                        var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();

                        if (jti is null || await blacklist.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                            context.Fail("The token has been revoked.");
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        // Slows down password guessing: by default 10 auth requests per minute per client IP
        var permitLimit = configuration.GetValue("Auth:RateLimitPerMinute", 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthController.RateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        return services;
    }
}
