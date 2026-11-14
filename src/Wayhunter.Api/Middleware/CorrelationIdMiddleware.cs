
using Wayhunter.Api.Contracts;

namespace Wayhunter.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        const string headerName =
            IdentityApiContract.CorrelationIdHeader;

        string? requestedId = null;

        if (context.Request.Headers.TryGetValue(
                headerName, out var values)
            && values.Count == 1)
        {
            requestedId = values[0];
        }

        string correlationId = IsSafe(requestedId)
            ? requestedId!
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[headerName] =
                correlationId;

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > 64)
        {
            return false;
        }

        return value.All(c =>
            (c >= 'A' && c <= 'Z') ||
            (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') ||
            c == '-' || c == '_' || c == '.');
    }
}
