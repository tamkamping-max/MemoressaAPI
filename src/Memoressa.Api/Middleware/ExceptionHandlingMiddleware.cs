using System.Net;
using System.Text.Json;
using Memoressa.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Memoressa.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();

            if (DatabaseBootstrap.IsConnectionFailure(ex))
            {
                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                context.Response.ContentType = "application/json";
                var hint = DatabaseBootstrap.GetConnectionFailureMessage(_configuration);
                var payload = JsonSerializer.Serialize(new
                {
                    error = "Database is unavailable.",
                    hint,
                    detail = _environment.IsDevelopment() ? ex.Message : null
                });
                await context.Response.WriteAsync(payload);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var genericPayload = JsonSerializer.Serialize(new
            {
                error = ex.Message,
                detail = _environment.IsDevelopment() ? ex.ToString() : null
            });
            await context.Response.WriteAsync(genericPayload);
        }
    }
}
