using System.Text.Json;
using TCM.Application.Common;

namespace TCM.Api.Middleware;

/// <summary>
/// Last line of defence. Anything that escapes a service becomes a logged 500 with a generic
/// body — stack traces and exception messages must never reach a client (SPEC section 7).
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // Too late to rewrite the response; the log above is all we can do.
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var body = ApiResponse<object>.Fail(
                "An unexpected error occurred. Please try again.",
                ErrorKind.External);

            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}
