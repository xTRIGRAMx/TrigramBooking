using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TrigramBooking.API.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Structured Logging
            _logger.LogError(
                exception,
                "Unhandled exception occurred on {Method} {Path}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);

            //2 Status Code
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            //3 standard ProblemDetails object
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Detail = _environment.IsDevelopment() ? exception.ToString() : null,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            };

            // Add custom extensions like TraceId
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            // 4 Write problem details response automatically
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            // Return true to signal that this exception was handled
            return true;
        }
    }
}
