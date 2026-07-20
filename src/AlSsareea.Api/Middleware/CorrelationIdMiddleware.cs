using Microsoft.Extensions.Primitives;

namespace AlSsareea.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = GetCorrelationId(context.Request.Headers[HeaderName]);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(StringValues values)
    {
        if (values.Count == 1)
        {
            string? candidate = values[0];
            if (candidate is not null && IsValid(candidate))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsValid(string value)
    {
        if (value.Length is < 1 or > MaximumLength)
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
