using Kitsuneo.Application.Speech;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kitsuneo.API.Infrastructure.ErrorHandling;

/// <summary>Turns a <see cref="TextToSpeechUnavailableException"/> into a 503 ProblemDetails response.</summary>
public sealed class TextToSpeechUnavailableExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not TextToSpeechUnavailableException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Text-to-speech unavailable",
                Detail = exception.Message
            }
        });
    }
}
