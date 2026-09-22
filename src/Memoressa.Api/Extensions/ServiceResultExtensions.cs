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

    private static IActionResult ToErrorResult(string error, int statusCode) =>
        statusCode switch
        {
            401 => new UnauthorizedObjectResult(new { error }),
            403 => new ObjectResult(new { error }) { StatusCode = 403 },
            404 => new NotFoundObjectResult(new { error }),
            409 => new ConflictObjectResult(new { error }),
            _ => new BadRequestObjectResult(new { error })
        };
}
