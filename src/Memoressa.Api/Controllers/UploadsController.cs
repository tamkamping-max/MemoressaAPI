using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IUploadService _uploadService;

    public UploadsController(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartUpload([FromBody] StartUploadRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _uploadService.StartUploadAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteUploads(CancellationToken cancellationToken)
    {
        var result = await _uploadService.GetIncompleteUploadsAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{sessionId:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(
        Guid sessionId,
        [FromBody] CompleteUploadRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _uploadService.CompleteUploadAsync(sessionId, request, cancellationToken);
        return result.ToActionResult();
    }
}
