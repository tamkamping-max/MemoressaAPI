using Memoressa.Application.Common;
using Memoressa.Application.DTOs;

namespace Memoressa.Application.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<ServiceResult> RequestPasswordResetAsync(PasswordResetRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> RequestPasswordResetCodeAsync(PasswordResetEmailRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> VerifyPasswordResetCodeAsync(PasswordResetCodeVerifyRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ConfirmPasswordResetWithCodeAsync(PasswordResetCodeConfirmRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ScheduleAccountDeletionAsync(string password, CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelAccountDeletionAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AccountDeletionStatusDto>> GetDeletionStatusAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> LoginWithGoogleAsync(OAuthLoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> LoginWithFacebookAsync(OAuthLoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> LoginWithAppleAsync(AppleOAuthRequestDto request, CancellationToken cancellationToken = default);
}

public interface IMemoryService
{
    Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetMemoriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetAiCuratedMemoriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetTodayMemoriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<MemoryDto>> RegenerateTodayHighlightAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetYearsAgoTodayMemoriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<MemoryDto>> GetMemoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<MemoryDto>> CreateMemoryAsync(CreateMemoryRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<MemoryDto>> UpdateMemoryAsync(Guid id, UpdateMemoryRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteMemoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<MemoryDto>>> FilterMemoriesAsync(MemoryFilterDto filter, CancellationToken cancellationToken = default);
}

public interface IPhotoService
{
    Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoDto>> GetPhotoByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosByDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosByMemberAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoDto>> UpdatePhotoAsync(Guid id, UpdatePhotoRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> HidePhotoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoTimelinePageDto>> GetTimelinePhotosAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoDownloadDto>> GetOriginalDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoDownloadBatchResponseDto>> GetDownloadBatchAsync(
        PhotoDownloadBatchRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoDeleteBatchResponseDto>> DeletePhotosBatchAsync(
        PhotoDeleteBatchRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<TodayMemoriesResponseDto>> GetTodayMemoriesAsync(
        TodayMemoriesRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<PhotoCommentDto>>> GetPhotoCommentsAsync(
        Guid photoId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoCommentDto>> AddPhotoCommentAsync(
        Guid photoId,
        AddPhotoCommentRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> DeletePhotoCommentAsync(
        Guid photoId,
        Guid commentId,
        CancellationToken cancellationToken = default);
}

public interface IFamilyService
{
    Task<ServiceResult<IReadOnlyList<FamilyMemberDto>>> GetMembersAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<FamilyMemberDto>> GetMemberByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<FamilyMemberDto>> AddMemberAsync(CreateFamilyMemberRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FamilyMemberDto>> UpdateMemberAsync(Guid id, UpdateFamilyMemberRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteMemberAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<FamilyMemberDto>>> GetMembersByGenerationAsync(Domain.Enums.Generation generation, CancellationToken cancellationToken = default);
}

public interface IFamilyMomentService
{
    Task<ServiceResult<IReadOnlyList<FamilyMomentDto>>> GetMomentsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<FamilyMomentDto>> CreateMomentAsync(CreateFamilyMomentRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FamilyMomentDto>> UpdateMomentAsync(Guid id, UpdateFamilyMomentRequestDto request, CancellationToken cancellationToken = default);
}

public interface IDisplayDeviceService
{
    Task<ServiceResult<IReadOnlyList<DisplayDeviceDto>>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<DisplayDeviceDto>> CreateDeviceAsync(CreateDisplayDeviceRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<DisplayDeviceDto>> BindDeviceAsync(BindDisplayDeviceRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<DisplayDeviceDto>> RenameDeviceAsync(Guid id, RenameDisplayDeviceRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> UnbindDeviceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult> SendMemoryToDeviceAsync(Guid deviceId, SendMemoryToDeviceRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<string>> GenerateQrCodeAsync(CancellationToken cancellationToken = default);
}

public interface IAiService
{
    Task<ServiceResult<AiAnalysisResultDto>> AnalyzePhotosAsync(AnalyzePhotosRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<SearchResultDto>>> SearchMemoriesAsync(string query, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<PlaybackItemDto>>> GeneratePlaybackAsync(PlaybackRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<MemoryDto>> CreateAiMemoryAsync(CreateAiMemoryRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ConfirmAiInferenceAsync(Guid photoId, Guid memberId, CancellationToken cancellationToken = default);
    Task<ServiceResult> RejectAiInferenceAsync(Guid photoId, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<ServiceResult<string>> GetLocaleAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> SetLocaleAsync(string locale, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> IsOnboardingCompleteAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> SetOnboardingCompleteAsync(bool value, CancellationToken cancellationToken = default);
    Task<ServiceResult<Dictionary<string, bool>>> GetAiSettingsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAiSettingsAsync(Dictionary<string, bool> settings, CancellationToken cancellationToken = default);
}

public interface IFriendService
{
    Task<ServiceResult<IReadOnlyList<FriendDto>>> GetFriendsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<FriendDto>> AddFriendAsync(CreateFriendRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FriendDto>> UpdateFriendAsync(Guid id, UpdateFriendRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteFriendAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IJournalTagService
{
    Task<ServiceResult<IReadOnlyList<JournalTagDto>>> GetTagsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<JournalTagDto>> CreateTagAsync(CreateJournalTagRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<JournalTagDto>> UpdateTagAsync(Guid id, UpdateJournalTagRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteTagAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPhotoUserTagLibraryService
{
    Task<ServiceResult<IReadOnlyList<PhotoUserTagLibraryEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoUserTagLibraryEntryDto>> CreateEntryAsync(
        CreatePhotoUserTagLibraryEntryRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPhotoAlbumService
{
    Task<ServiceResult<PhotoAlbumDto>> CreateOrFindAsync(
        CreatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoAlbumDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoAlbumListPageDto>> ListCardsAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoAlbumDto>> UpdateAsync(
        Guid id,
        UpdatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoAlbumDto>> PatchPhotosAsync(
        Guid id,
        PatchPhotoAlbumPhotosRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoAlbumDto>> UnlinkPhotosAsync(
        Guid id,
        UnlinkPhotoAlbumPhotosRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>> GetCommentsAsync(
        Guid albumId,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoAlbumCommentDto>> AddCommentAsync(
        Guid albumId,
        AddPhotoAlbumCommentRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteCommentAsync(
        Guid albumId,
        Guid commentId,
        CancellationToken cancellationToken = default);
    Task<(Guid AlbumId, IReadOnlyList<string> UserTags, string? Description)?> TryGetPrimaryAlbumForPhotoAsync(
        Guid photoId,
        Guid familyId,
        CancellationToken cancellationToken = default);
}

public interface IActivityService
{
    Task<ServiceResult<ApiDataResponseDto<ActivityAlbumListDataDto>>> GetInProgressAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<ActivityAlbumDto>> CreateAsync(UpsertActivityAlbumRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ActivityAlbumDto>> UpdateAsync(string activityId, UpsertActivityAlbumRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>> GetActiveTodayAsync(
        DateOnly? date,
        int? limit = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> AttachPhotosAsync(string activityId, ActivityAlbumPhotosRequestDto request, CancellationToken cancellationToken = default);
    Task LinkPhotoAfterUploadAsync(Guid activityAlbumId, Guid photoId, Guid familyId, CancellationToken cancellationToken = default);
}

public interface ISharedAlbumService
{
    Task<ServiceResult<IReadOnlyList<SharedAlbumDto>>> GetAlbumsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<SharedAlbumDto>> GetAlbumByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<ServiceResult<SharedAlbumDto>> CreateAlbumAsync(CreateSharedAlbumRequestDto request, CancellationToken cancellationToken = default);
}

public interface IUploadService
{
    Task<ServiceResult<StartUploadResponseDto>> StartUploadAsync(StartUploadRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<PhotoDto>> CompleteUploadAsync(
        Guid sessionId,
        CompleteUploadRequestDto? request = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StorageUsageDto>> GetStorageUsageAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<IncompleteUploadSessionDto>>> GetIncompleteUploadsAsync(CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<ServiceResult<IReadOnlyList<NotificationDto>>> GetNotificationsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}

public interface IFrameService
{
    Task<ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>> GetPlaybackPackagesAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FramePlaybackPackageDto>> CreatePlaybackPackageAsync(Guid deviceId, CreatePlaybackPackageRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<FramePlaybackPackageDto>> EnsurePlaybackPackageAsync(Guid deviceId, EnsurePlaybackPackageRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<FrameCommentDto>>> GetCommentsAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FrameCommentDto>> AddCommentAsync(Guid packageId, AddFrameCommentRequestDto request, CancellationToken cancellationToken = default);
}

public interface IInternalRealtimeService
{
    Task<ServiceResult> UpdateDeviceStatusAsync(UpdateDeviceStatusRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<FrameCommandDto>>> GetPendingCommandsAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<ServiceResult> AcknowledgeCommandAsync(Guid commandId, AckFrameCommandRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>> GetDevicePlaybackPackagesAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
