using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class FriendService : IFriendService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FriendService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<FriendDto>>> GetFriendsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<FriendDto>>.Fail("Unauthorized", 401);
        }

        var friends = await _db.Friends.AsNoTracking()
            .Where(f => f.OwnerUserId == _currentUser.UserId.Value)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FriendDto>>.Ok(friends.Select(f => f.ToDto()).ToList());
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
        return ServiceResult<FriendDto>.Ok(friend.ToDto());
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

        var friend = await _db.Friends.FirstOrDefaultAsync(
            f => f.Id == id && f.OwnerUserId == _currentUser.UserId.Value,
            cancellationToken);

        if (friend is null)
        {
            return ServiceResult.NotFound("Friend not found");
        }

        _db.Friends.Remove(friend);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
