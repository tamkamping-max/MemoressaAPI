using Memoressa.Api.Extensions;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/storage")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly IUploadService _uploadService;

    public StorageController(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetStorageUsageAsync(cancellationToken);
        return result.ToActionResult();
    }
}
