using Memoressa.Application.DTOs;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class PhotoPrivacyValidation
{
    public const string OnlySelfVisibilityConflictMessage =
        "privacyScope onlySelf cannot be combined with visibility family";

    public const string CustomRequiresAudienceMessage =
        "privacyScope custom requires at least one memberId or friendId";

    public const string ActivityParticipantsLockedMessage =
        "activityParticipantsVisible cannot be disabled for photos linked to an activity album";

    public static ServiceResult? ValidateScopeAndVisibility(
        UploadPrivacyScope privacyScope,
        MemoryVisibility? visibility)
    {
        if (privacyScope == UploadPrivacyScope.OnlySelf
            && visibility == MemoryVisibility.Family)
        {
            return ServiceResult.Fail(OnlySelfVisibilityConflictMessage, 400);
        }

        return null;
    }

    public static ServiceResult? ValidateCustomAudience(
        UploadPrivacyScope privacyScope,
        IReadOnlyList<Guid>? memberIds,
        IReadOnlyList<Guid>? friendIds)
    {
        if (privacyScope != UploadPrivacyScope.Custom)
        {
            return null;
        }

        var memberCount = memberIds?.Count(id => id != Guid.Empty) ?? 0;
        var friendCount = friendIds?.Count(id => id != Guid.Empty) ?? 0;
        if (memberCount + friendCount == 0)
        {
            return ServiceResult.Fail(CustomRequiresAudienceMessage, 400);
        }

        return null;
    }

    public static ServiceResult? ValidateStartUpload(StartUploadRequestDto request, bool hasActivityAlbum)
    {
        var scopeFailure = ValidateScopeAndVisibility(request.PrivacyScope, request.Visibility);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        var customFailure = ValidateCustomAudience(
            request.PrivacyScope,
            request.MemberIds,
            request.FriendIds);
        if (customFailure is not null)
        {
            return customFailure;
        }

        if (!hasActivityAlbum
            && request.ActivityParticipantsVisible == true
            && request.PrivacyScope == UploadPrivacyScope.OnlySelf)
        {
            // Allowed: explicit opt-in without activity is harmless (no linked album to grant access).
        }

        return null;
    }

    public static ServiceResult? ValidateCompleteOrUpdate(
        UploadPrivacyScope privacyScope,
        MemoryVisibility? visibility,
        IReadOnlyList<Guid>? memberIds,
        IReadOnlyList<Guid>? friendIds)
    {
        var scopeFailure = ValidateScopeAndVisibility(privacyScope, visibility);
        if (scopeFailure is not null)
        {
            return scopeFailure;
        }

        return ValidateCustomAudience(privacyScope, memberIds, friendIds);
    }
}
