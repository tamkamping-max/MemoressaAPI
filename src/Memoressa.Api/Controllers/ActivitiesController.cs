using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/activities")]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activities;

    public ActivitiesController(IActivityService activities)
    {
        _activities = activities;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? excludeStatus,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _activities.ListAsync(status, excludeStatus, limit, cursor, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("in-progress")]
    public async Task<IActionResult> GetInProgress(CancellationToken cancellationToken)
    {
        var result = await _activities.GetInProgressAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("active-today")]
    public async Task<IActionResult> GetActiveToday(
        [FromQuery] DateOnly? date,
        [FromQuery] int? limit,
        [FromQuery] int? photoLimit,
        CancellationToken cancellationToken)
    {
        var result = await _activities.GetActiveTodayAsync(date, limit, photoLimit, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{activityId}")]
    public async Task<IActionResult> GetById(
        string activityId,
        [FromQuery] Guid? creatorUserId,
        CancellationToken cancellationToken)
    {
        var result = await _activities.GetByIdAsync(activityId, creatorUserId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{activityId}/photos")]
    public async Task<IActionResult> GetPhotos(
        string activityId,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        [FromQuery] Guid? creatorUserId,
        CancellationToken cancellationToken)
    {
        var result = await _activities.GetActivityPhotosAsync(
            activityId,
            limit,
            creatorUserId,
            cursor,
            cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{activityId}/photos")]
    public async Task<IActionResult> ReplacePhotos(
        string activityId,
        [FromBody] ActivityAlbumPhotosRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _activities.ReplaceActivityPhotosAsync(activityId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertActivityAlbumRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _activities.CreateAsync(request, cancellationToken);
        if (!result.Success)
        {
            return result.ToActionResult();
        }

        return result.StatusCode switch
        {
            201 => new ObjectResult(new ApiDataResponseDto<ActivityAlbumDto> { Data = result.Data! })
            {
                StatusCode = 201
            },
            _ => new OkObjectResult(new ApiDataResponseDto<ActivityAlbumDto> { Data = result.Data! })
        };
    }

    [HttpPut("{activityId}")]
    public async Task<IActionResult> Update(
        string activityId,
        [FromBody] UpsertActivityAlbumRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _activities.UpdateAsync(activityId, request, cancellationToken);
        if (!result.Success)
        {
            return result.ToActionResult();
        }

        return new OkObjectResult(new ApiDataResponseDto<ActivityAlbumDto> { Data = result.Data! });
    }

    [HttpDelete("{activityId}")]
    public async Task<IActionResult> Delete(string activityId, CancellationToken cancellationToken)
    {
        var result = await _activities.DeleteAsync(activityId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{activityId}/photos")]
    public async Task<IActionResult> AttachPhotos(
        string activityId,
        [FromBody] ActivityAlbumPhotosRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _activities.AttachPhotosAsync(activityId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{activityId}/photos/link")]
    public async Task<IActionResult> LinkPhotos(
        string activityId,
        [FromBody] ActivityAlbumPhotosRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _activities.AttachPhotosAsync(activityId, request, cancellationToken);
        return result.ToActionResult();
    }
}
