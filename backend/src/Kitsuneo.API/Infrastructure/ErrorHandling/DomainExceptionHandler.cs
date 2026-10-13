using Kitsuneo.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Infrastructure.ErrorHandling;

/// <summary>
/// Turns a <see cref="DomainException"/> into a ProblemDetails response:
/// 409 for a <see cref="DomainConflictException"/>, 400 for any other.
/// </summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
            return false;

        var (status, title) = domainException is DomainConflictException
            ? (StatusCodes.Status409Conflict, "Conflict")
            : (StatusCodes.Status400BadRequest, "Invalid request");

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = domainException.Message
            }
        });
    }
}
