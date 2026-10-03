using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class FriendService : IFriendService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAvatarUrlResolver _avatarUrls;

    public FriendService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IAvatarUrlResolver avatarUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _avatarUrls = avatarUrls;
    }

    public async Task<ServiceResult<IReadOnlyList<FriendDto>>> GetFriendsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<FriendDto>>.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<IReadOnlyList<FriendDto>>.Fail("Unauthorized", 401);
        }

        var friends = await _db.Friends.AsNoTracking()
            .Where(f => f.OwnerUserId == userId)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var dtos = new List<FriendDto>();

        if (friends.Count > 0)
        {
            var friendIds = friends.Select(f => f.Id).ToList();
            var activityCounts = await _db.ActivityAlbumFriends.AsNoTracking()
                .Where(af => af.FriendId != null && friendIds.Contains(af.FriendId.Value))
                .GroupBy(af => af.FriendId!.Value)
                .Select(g => new { FriendId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);
            var activityCountMap = activityCounts.ToDictionary(x => x.FriendId, x => x.Count);

            var linkedUserIds = friends
                .Where(f => f.FriendUserId.HasValue)
                .Select(f => f.FriendUserId!.Value)
                .Distinct()
                .ToList();
            var linkedUsers = linkedUserIds.Count == 0
                ? new Dictionary<Guid, UserAccount>()
                : await _db.UserAccounts.AsNoTracking()
                    .Where(u => linkedUserIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, cancellationToken);

            foreach (var f in friends)
            {
                linkedUsers.TryGetValue(f.FriendUserId ?? Guid.Empty, out var linked);
                var activityCount = activityCountMap.GetValueOrDefault(f.Id);
                dtos.Add(await MapAcceptedFriendDtoAsync(f, linked, activityCount, cancellationToken));
            }
        }

        dtos.AddRange(await BuildPendingFriendDtosAsync(userId, user.Email, cancellationToken));

        return ServiceResult<IReadOnlyList<FriendDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<FriendDto>> AddFriendAsync(
        CreateFriendRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<FriendDto>.Fail("Unauthorized", 401);
        }

        var friend = new Friend
        {
            OwnerUserId = _currentUser.UserId.Value,
            FriendUserId = request.FriendUserId,
            Name = request.Name,
            AvatarUrl = request.AvatarUrl
        };

        _db.Friends.Add(friend);
        await _db.SaveChangesAsync(cancellationToken);

        UserAccount? linkedUser = null;
        if (friend.FriendUserId.HasValue)
        {
            linkedUser = await _db.UserAccounts.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == friend.FriendUserId.Value, cancellationToken);
        }

        return ServiceResult<FriendDto>.Ok(
            await MapAcceptedFriendDtoAsync(friend, linkedUser, activityCount: 0, cancellationToken));
    }

    public async Task<ServiceResult<FriendDto>> UpdateFriendAsync(
        Guid id,
        UpdateFriendRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<FriendDto>.Fail("Unauthorized", 401);
        }

        var friend = await _db.Friends.FirstOrDefaultAsync(
            f => f.Id == id && f.OwnerUserId == _currentUser.UserId.Value,
            cancellationToken);

        if (friend is null)
        {
            return ServiceResult<FriendDto>.NotFound("Friend not found");
        }

        if (request.Name is not null)
        {
            friend.Name = request.Name;
        }

        if (request.AvatarUrl is not null)
        {
            friend.AvatarUrl = request.AvatarUrl;
        }

        if (request.FrameLinked.HasValue)
        {
            friend.FrameLinked = request.FrameLinked.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<FriendDto>.Ok(friend.ToDto());
    }

    public async Task<ServiceResult> DeleteFriendAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;

        var friend = await _db.Friends.FirstOrDefaultAsync(
            f => f.Id == id && f.OwnerUserId == userId,
            cancellationToken);

        if (friend is not null)
        {
            await RemoveAcceptedFriendConnectionAsync(userId, friend, cancellationToken);
            return ServiceResult.NoContent();
        }

        var invite = await _db.FriendInvites.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (invite is null || invite.Status != Domain.Enums.FriendInviteStatus.Pending)
        {
            return ServiceResult.NotFound("Friend not found");
        }

        if (invite.InviterUserId == userId)
        {
            invite.Status = Domain.Enums.FriendInviteStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.NoContent();
        }

        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        if (invite.InviteeUserId == userId
            || string.Equals(invite.InviteeEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            invite.Status = Domain.Enums.FriendInviteStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.NoContent();
        }

        return ServiceResult.Forbidden("You cannot delete this connection");
    }

    private async Task RemoveAcceptedFriendConnectionAsync(
        Guid ownerUserId,
        Friend friend,
        CancellationToken cancellationToken)
    {
        if (friend.FriendUserId.HasValue)
        {
            var reciprocal = await _db.Friends
                .Where(f => f.OwnerUserId == friend.FriendUserId.Value && f.FriendUserId == ownerUserId)
                .ToListAsync(cancellationToken);
            if (reciprocal.Count > 0)
            {
                _db.Friends.RemoveRange(reciprocal);
            }
        }

        _db.Friends.Remove(friend);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
