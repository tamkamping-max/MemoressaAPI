using Memoressa.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Extensions;

public static class ServiceResultExtensions
{
    public static IActionResult ToActionResult(this ServiceResult result)
    {
        if (result.Success)
        {
            return result.StatusCode switch
            {
                204 => new NoContentResult(),
                201 => new StatusCodeResult(201),
                _ => new OkResult()
            };
        }

        return ToErrorResult(result.Error ?? "Request failed", result.StatusCode);
    }

    public static IActionResult ToActionResult<T>(this ServiceResult<T> result)
    {
        if (result.Success)
        {
            return result.StatusCode switch
            {
                201 => new ObjectResult(result.Data) { StatusCode = 201 },
                204 => new NoContentResult(),
                _ => new OkObjectResult(result.Data)
            };
        }

        return ToErrorResult(result.Error ?? "Request failed", result.StatusCode);
    }

    private static IActionResult ToErrorResult(string error, int statusCode)
    {
        var body = new { error, message = error };
        return statusCode switch
        {
            401 => new UnauthorizedObjectResult(body),
            403 => new ObjectResult(body) { StatusCode = 403 },
            404 => new NotFoundObjectResult(body),
            409 => new ConflictObjectResult(body),
            413 => new ObjectResult(body) { StatusCode = 413 },
            429 => new ObjectResult(body) { StatusCode = 429 },
            503 => new ObjectResult(body) { StatusCode = 503 },
            204 => new StatusCodeResult(204),
            _ => new BadRequestObjectResult(body)
        };
    }
}
