using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Application.Services;

public partial class ProfileService : IProfileService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IS3StorageService _s3;
    private readonly MediaStorageSettings _storageSettings;
    private readonly IAvatarUrlResolver _avatarUrls;

    public ProfileService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IS3StorageService s3,
        IOptions<MediaStorageSettings> storageSettings,
        IAvatarUrlResolver avatarUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _s3 = s3;
        _storageSettings = storageSettings.Value;
        _avatarUrls = avatarUrls;
    }

    public async Task<ServiceResult<AvatarUploadStartResponseDto>> StartAvatarUploadAsync(
        AvatarUploadStartRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<AvatarUploadStartResponseDto>.Fail("Unauthorized", 401);
        }

        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<AvatarUploadStartResponseDto>.Fail("Unauthorized", 401);
        }

        var contentType = (request.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/jpg"))
        {
            return ServiceResult<AvatarUploadStartResponseDto>.Fail("Only image/jpeg is supported", 400);
        }

        var purpose = (request.Purpose ?? string.Empty).Trim().ToLowerInvariant();
        if (purpose is not ("user_profile" or "family_member"))
        {
            return ServiceResult<AvatarUploadStartResponseDto>.Fail("Invalid purpose", 400);
        }

        var userId = _currentUser.UserId.Value;
        var familyId = ctx.Value.FamilyId;
        string avatarKey;

        if (purpose == "family_member")
        {
            if (!request.FamilyMemberId.HasValue)
            {
                return ServiceResult<AvatarUploadStartResponseDto>.Fail("familyMemberId is required", 400);
            }

            var memberExists = await _db.FamilyMembers.AsNoTracking()
                .AnyAsync(
                    m => m.Id == request.FamilyMemberId.Value && m.FamilyId == familyId,
                    cancellationToken);
            if (!memberExists)
            {
                return ServiceResult<AvatarUploadStartResponseDto>.Fail("Family member not found", 404);
            }

            avatarKey = BuildAvatarKey($"avatars/families/{familyId}/members/{request.FamilyMemberId.Value}");
        }
        else
        {
            avatarKey = BuildAvatarKey($"avatars/users/{userId}");
        }

        var expiryMinutes = _storageSettings.UploadPresignedUrlExpiryMinutes;
        var expiry = TimeSpan.FromMinutes(expiryMinutes);
        var uploadUrl = await _s3.GetPresignedPutUrlAsync(avatarKey, "image/jpeg", expiry, cancellationToken);

        return ServiceResult<AvatarUploadStartResponseDto>.Ok(new AvatarUploadStartResponseDto
        {
            UploadUrl = uploadUrl,
            AvatarUrl = avatarKey
        });
    }

    private string BuildAvatarKey(string folder)
    {
        var prefix = string.IsNullOrWhiteSpace(_storageSettings.KeyPrefix)
            ? string.Empty
            : _storageSettings.KeyPrefix.TrimEnd('/') + "/";
        return $"{prefix}{folder}/{Guid.NewGuid():N}.jpg";
    }
}
