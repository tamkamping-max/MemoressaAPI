using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/photo-albums")]
[Authorize]
public class PhotoAlbumsController : ControllerBase
{
    private readonly IPhotoAlbumService _photoAlbums;

    public PhotoAlbumsController(IPhotoAlbumService photoAlbums)
    {
        _photoAlbums = photoAlbums;
    }

    [HttpGet]
    public async Task<IActionResult> ListAlbumCards(
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.ListCardsAsync(limit, cursor, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrFind(
        [FromBody] CreatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.CreateOrFindAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>Alias for <see cref="CreateOrFind"/> (find-or-create by photo set).</summary>
    [HttpPost("ensure")]
    public Task<IActionResult> Ensure(
        [FromBody] CreatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken)
        => CreateOrFind(request, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.DeleteAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("{id:guid}/photos")]
    public async Task<IActionResult> PatchPhotos(
        Guid id,
        [FromBody] PatchPhotoAlbumPhotosRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.PatchPhotosAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{albumId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid albumId, CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.GetCommentsAsync(albumId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{albumId:guid}/comments")]
    public async Task<IActionResult> AddComment(
        Guid albumId,
        [FromBody] AddPhotoAlbumCommentRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.AddCommentAsync(albumId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{albumId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(
        Guid albumId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var result = await _photoAlbums.DeleteCommentAsync(albumId, commentId, cancellationToken);
        return result.ToActionResult();
    }
}
