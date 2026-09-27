using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/photos")]
[Authorize]
public class PhotosController : ControllerBase
{
    private readonly IPhotoService _photoService;

    public PhotosController(IPhotoService photoService)
    {
        _photoService = photoService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPhotos(CancellationToken cancellationToken)
    {
        var result = await _photoService.GetPhotosAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPhotoById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _photoService.GetPhotoByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("by-date/{date:datetime}")]
    public async Task<IActionResult> GetPhotosByDate(DateTime date, CancellationToken cancellationToken)
    {
        var result = await _photoService.GetPhotosByDateAsync(date, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("by-member/{memberId:guid}")]
    public async Task<IActionResult> GetPhotosByMember(Guid memberId, CancellationToken cancellationToken)
    {
        var result = await _photoService.GetPhotosByMemberAsync(memberId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadOriginal(Guid id, CancellationToken cancellationToken)
    {
        var result = await _photoService.GetOriginalDownloadAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken cancellationToken)
    {
        var result = await _photoService.DeletePhotoAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePhoto(Guid id, [FromBody] UpdatePhotoRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _photoService.UpdatePhotoAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/hide")]
    public async Task<IActionResult> HidePhoto(Guid id, CancellationToken cancellationToken)
    {
        var result = await _photoService.HidePhotoAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimelinePhotos(
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _photoService.GetTimelinePhotosAsync(limit, cursor, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Today memories for the family (MemoressaApp: home + 回憶 tab). Cached per calendar day.
    /// </summary>
    [HttpPost("today-memories")]
    public async Task<IActionResult> GetTodayMemories(
        [FromBody] TodayMemoriesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _photoService.GetTodayMemoriesAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{photoId:guid}/comments")]
    public async Task<IActionResult> GetPhotoComments(Guid photoId, CancellationToken cancellationToken)
    {
        var result = await _photoService.GetPhotoCommentsAsync(photoId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{photoId:guid}/comments")]
    public async Task<IActionResult> AddPhotoComment(
        Guid photoId,
        [FromBody] AddPhotoCommentRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _photoService.AddPhotoCommentAsync(photoId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{photoId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeletePhotoComment(
        Guid photoId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var result = await _photoService.DeletePhotoCommentAsync(photoId, commentId, cancellationToken);
        return result.ToActionResult();
    }
}
