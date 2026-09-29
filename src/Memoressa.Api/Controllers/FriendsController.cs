using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendService _friendService;

    public FriendsController(IFriendService friendService)
    {
        _friendService = friendService;
    }

    [HttpGet]
    public async Task<IActionResult> GetFriends(CancellationToken cancellationToken)
    {
        var result = await _friendService.GetFriendsAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("invites")]
    public async Task<IActionResult> GetFriendInvites(CancellationToken cancellationToken)
    {
        var result = await _friendService.GetFriendInvitesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("invites")]
    public async Task<IActionResult> CreateFriendInvite(
        [FromBody] CreateFriendInviteRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _friendService.CreateFriendInviteAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("invites/{id:guid}/accept")]
    public async Task<IActionResult> AcceptFriendInvite(Guid id, CancellationToken cancellationToken)
    {
        var result = await _friendService.AcceptFriendInviteAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("invites/{id:guid}/reject")]
    public async Task<IActionResult> RejectFriendInvite(Guid id, CancellationToken cancellationToken)
    {
        var result = await _friendService.RejectFriendInviteAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> AddFriend([FromBody] CreateFriendRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _friendService.AddFriendAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateFriend(Guid id, [FromBody] UpdateFriendRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _friendService.UpdateFriendAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFriend(Guid id, CancellationToken cancellationToken)
    {
        var result = await _friendService.DeleteFriendAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
