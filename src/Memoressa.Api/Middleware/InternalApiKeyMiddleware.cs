using System.Text.Json;
using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Memoressa.Api.Middleware;

public class InternalApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-Internal-Api-Key";
    private readonly RequestDelegate _next;
    private readonly InternalApiOptions _options;

    public InternalApiKeyMiddleware(RequestDelegate next, IOptions<InternalApiOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedKey)
            || string.IsNullOrWhiteSpace(providedKey)
            || !string.Equals(providedKey.ToString(), _options.ApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Invalid or missing internal API key" }));
            return;
        }

        await _next(context);
    }
}
