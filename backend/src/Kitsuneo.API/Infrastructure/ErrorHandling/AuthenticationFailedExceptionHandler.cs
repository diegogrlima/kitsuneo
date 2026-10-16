using Kitsuneo.Application.Auth;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Infrastructure.ErrorHandling;

/// <summary>Turns an <see cref="AuthenticationFailedException"/> into a 401 ProblemDetails response.</summary>
public sealed class AuthenticationFailedExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not AuthenticationFailedException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = exception.Message
            }
        });
    }
}
