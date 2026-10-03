using Memoressa.Domain.Common;
using Memoressa.Domain.Enums;

namespace Memoressa.Domain.Entities;

public class UserAccount : Entity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public Generation? Generation { get; set; }
    public string? ProfileCityId { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public bool OnboardingComplete { get; set; }
    public string Locale { get; set; } = "en";
    public DateTime? DeletionScheduledAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Sum of original (full) photo bytes stored for this user.</summary>
    public long CloudStorageUsedBytes { get; set; }
    /// <summary>Family member representing this user ("this is me") for face recognition sync.</summary>
    public Guid? SelfFamilyMemberId { get; set; }
    public FamilyMember? SelfFamilyMember { get; set; }

    public ICollection<UserOAuthLink> OAuthLinks { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<FamilyMembership> FamilyMemberships { get; set; } = [];
    public ICollection<UserAiSetting> AiSettings { get; set; } = [];
}

public class UserOAuthLink : Entity
{
    public Guid UserId { get; set; }
    public OAuthProvider Provider { get; set; }
    public string ProviderUserId { get; set; } = string.Empty;
    public UserAccount User { get; set; } = null!;
}

public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class PasswordResetToken : Entity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class PasswordResetCode : Entity
{
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedVerifyAttempts { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class EmailVerificationCode : Entity
{
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedVerifyAttempts { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class EmailChangeCode : Entity
{
    public Guid UserId { get; set; }
    public string NewEmail { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int FailedVerifyAttempts { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class FriendInvite : Entity
{
    public Guid InviterUserId { get; set; }
    public Guid? InviteeUserId { get; set; }
    public string InviteeEmail { get; set; } = string.Empty;
    public FriendInviteStatus Status { get; set; } = FriendInviteStatus.Pending;
    public UserAccount Inviter { get; set; } = null!;
    public UserAccount? Invitee { get; set; }
}

public class Family : Entity
{
    public string Name { get; set; } = "My Family";
    public Guid OwnerUserId { get; set; }
    public UserAccount Owner { get; set; } = null!;

    public ICollection<FamilyMembership> Memberships { get; set; } = [];
    public ICollection<FamilyMember> Members { get; set; } = [];
    public ICollection<Photo> Photos { get; set; } = [];
    public ICollection<Memory> Memories { get; set; } = [];
    public ICollection<FamilyMoment> Moments { get; set; } = [];
    public ICollection<DisplayDevice> DisplayDevices { get; set; } = [];
    public ICollection<SharedAlbum> SharedAlbums { get; set; } = [];
}

public class FamilyMembership : Entity
{
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "member";
    public Family Family { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public class FamilyMember : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public DateTime? BirthDate { get; set; }
    public Generation Generation { get; set; }
    public string? Relationship { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CityId { get; set; }
    public bool FaceRecognitionEnabled { get; set; } = true;
    public Guid? LinkedUserId { get; set; }

    public Family Family { get; set; } = null!;
    public ICollection<PhotoMember> PhotoMembers { get; set; } = [];
    public ICollection<MemoryMember> MemoryMembers { get; set; } = [];
}

public class Photo : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public Guid UploadedByUserId { get; set; }
    /// <summary>Local device path for demo/offline assets only.</summary>
    public string? LocalAssetPath { get; set; }
    /// <summary>Compressed JPEG for in-app display (e.g. abc.jpg).</summary>
    public string? S3Key { get; set; }
    /// <summary>True original object key (e.g. IMG_1234.HEIC). Preserved format for download.</summary>
    public string? S3KeyFull { get; set; }
    /// <summary>Original file name for download (e.g. IMG_1234.HEIC).</summary>
    public string? OriginalFileName { get; set; }
    /// <summary>MIME type of the true original (e.g. image/heic).</summary>
    public string? OriginalContentType { get; set; }
    public bool IsLivePhoto { get; set; }
    public string? LivePhotoVideoS3Key { get; set; }
    public string? LivePhotoVideoFileName { get; set; }
    public string? LivePhotoVideoContentType { get; set; }
    /// <summary>Thumbnail nail JPEG object key.</summary>
    public string? ThumbnailS3Key { get; set; }
    /// <summary>Legacy column — do not persist public URLs for private buckets.</summary>
    public string? RemoteUrl { get; set; }
    /// <summary>Legacy column — do not persist public URLs for private buckets.</summary>
    public string? ThumbnailUrl { get; set; }
    public DateTime? TakenAt { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? EventId { get; set; }
    public Generation? Generation { get; set; }
    public bool IsHidden { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsSimilar { get; set; }
    public bool IsBlurry { get; set; }
    public bool IsScreenshot { get; set; }
    public bool IsAiInferred { get; set; }
    public MemoryVisibility Visibility { get; set; } = MemoryVisibility.Family;
    public UploadPrivacyScope PrivacyScope { get; set; } = UploadPrivacyScope.Family;
    /// <summary>When true, users who can access a linked activity album may also view this photo (stacked with privacyScope).</summary>
    public bool ActivityParticipantsVisible { get; set; }
    public Guid? SharedAlbumId { get; set; }
    /// <summary>Total bytes counted toward user quota (still original + Live video when applicable).</summary>
    public long? FileSizeBytes { get; set; }
    /// <summary>Still original size component of <see cref="FileSizeBytes"/>.</summary>
    public long? OriginalStillFileSizeBytes { get; set; }
    public long LivePhotoVideoFileSizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? AiAnalysisJson { get; set; }

    public Family Family { get; set; } = null!;
    public UserAccount UploadedBy { get; set; } = null!;
    public SharedAlbum? SharedAlbum { get; set; }
    public ICollection<PhotoMember> PhotoMembers { get; set; } = [];
    public ICollection<PhotoFriend> PhotoFriends { get; set; } = [];
    public ICollection<PhotoAiTag> AiTags { get; set; } = [];
    public ICollection<PhotoUserTag> UserTags { get; set; } = [];
    public ICollection<PhotoComment> Comments { get; set; } = [];
    public ICollection<PhotoAiInference> AiInferences { get; set; } = [];
    public ICollection<MemoryPhoto> MemoryPhotos { get; set; } = [];
}

public class PhotoMember : Entity
{
    public Guid PhotoId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public Photo Photo { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public class PhotoFriend : Entity
{
    public Guid PhotoId { get; set; }
    public Guid FriendId { get; set; }
    public Photo Photo { get; set; } = null!;
    public Friend Friend { get; set; } = null!;
}

public class PhotoAiTag : Entity
{
    public Guid PhotoId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public Photo Photo { get; set; } = null!;
}

public class PhotoUserTag : Entity
{
    public Guid PhotoId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public Photo Photo { get; set; } = null!;
}

/// <summary>
/// Per-user reusable photo tag labels (tags sheet). Distinct from tags assigned on a photo (<see cref="PhotoUserTag"/>).
/// </summary>
public class UserPhotoTagLibraryEntry : Entity
{
    public Guid UserId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public UserAccount User { get; set; } = null!;
}

public class PhotoAlbum : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? Description { get; set; }
    public MemoryVisibility Visibility { get; set; } = MemoryVisibility.Family;
    public Guid? CoverPhotoId { get; set; }
    /// <summary>Sorted photo-id set key for find-or-create within a family.</summary>
    public string PhotoSetFingerprint { get; set; } = string.Empty;

    public Family Family { get; set; } = null!;
    public UserAccount CreatedBy { get; set; } = null!;
    public ICollection<PhotoAlbumPhoto> AlbumPhotos { get; set; } = [];
    public ICollection<PhotoAlbumUserTag> UserTags { get; set; } = [];
    public ICollection<PhotoAlbumMember> AlbumMembers { get; set; } = [];
    public ICollection<PhotoAlbumComment> Comments { get; set; } = [];
}

public class PhotoAlbumPhoto : Entity
{
    public Guid PhotoAlbumId { get; set; }
    public Guid PhotoId { get; set; }
    public int SortOrder { get; set; }
    public PhotoAlbum PhotoAlbum { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}

public class PhotoAlbumUserTag : Entity
{
    public Guid PhotoAlbumId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public PhotoAlbum PhotoAlbum { get; set; } = null!;
}

public class PhotoAlbumMember : Entity
{
    public Guid PhotoAlbumId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public PhotoAlbum PhotoAlbum { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public class PhotoAlbumComment : Entity
{
    public Guid PhotoAlbumId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public PhotoAlbum PhotoAlbum { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public class PhotoComment : Entity
{
    public Guid PhotoId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Photo Photo { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public class PhotoAiInference : Entity
{
    public Guid PhotoId { get; set; }
    public Guid? SuggestedMemberId { get; set; }
    public string? SuggestedMemberName { get; set; }
    public AiInferenceStatus Status { get; set; } = AiInferenceStatus.Pending;
    public double Confidence { get; set; }
    public Photo Photo { get; set; } = null!;
    public FamilyMember? SuggestedMember { get; set; }
}

public class Memory : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MemoryType Type { get; set; }
    public string? TextContent { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Location { get; set; }
    public EventType? EventType { get; set; }
    public Generation? Generation { get; set; }
    public bool IsAiGenerated { get; set; }
    public bool IsTodayHighlight { get; set; }
    public string? BackgroundMusicId { get; set; }
    public MemoryVisibility Visibility { get; set; } = MemoryVisibility.Family;
    public string? WeatherSummary { get; set; }

    public Family Family { get; set; } = null!;
    public UserAccount CreatedBy { get; set; } = null!;
    public ICollection<MemoryPhoto> MemoryPhotos { get; set; } = [];
    public ICollection<MemoryVideo> MemoryVideos { get; set; } = [];
    public ICollection<MemoryMember> MemoryMembers { get; set; } = [];
}

public class MemoryPhoto : Entity
{
    public Guid MemoryId { get; set; }
    public Guid PhotoId { get; set; }
    public int SortOrder { get; set; }
    public Memory Memory { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}

public class MemoryVideo : Entity
{
    public Guid MemoryId { get; set; }
    public Guid PhotoId { get; set; }
    public int SortOrder { get; set; }
    public Memory Memory { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}

public class MemoryMember : Entity
{
    public Guid MemoryId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public Memory Memory { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public class FamilyMoment : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public bool IsAiDiscovered { get; set; }

    public Family Family { get; set; } = null!;
    public ICollection<FamilyMomentPhoto> MomentPhotos { get; set; } = [];
    public ICollection<FamilyMomentMember> MomentMembers { get; set; } = [];
}

public class FamilyMomentPhoto : Entity
{
    public Guid FamilyMomentId { get; set; }
    public Guid PhotoId { get; set; }
    public FamilyMoment FamilyMoment { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}

public class FamilyMomentMember : Entity
{
    public Guid FamilyMomentId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public FamilyMoment FamilyMoment { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public class DisplayDevice : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public Guid? BoundByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public DisplayDeviceStatus Status { get; set; } = DisplayDeviceStatus.Offline;
    public Guid? CurrentMemoryId { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? SettingsJson { get; set; }

    public Family Family { get; set; } = null!;
    public Memory? CurrentMemory { get; set; }
    public ICollection<FrameCommand> Commands { get; set; } = [];
    public ICollection<FramePlaybackPackage> PlaybackPackages { get; set; } = [];
}

public class FrameCommand : Entity
{
    public Guid DisplayDeviceId { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public FrameCommandType CommandType { get; set; }
    public FrameCommandStatus Status { get; set; } = FrameCommandStatus.Pending;
    public string PayloadJson { get; set; } = "{}";
    public DateTime? DeliveredAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DisplayDevice DisplayDevice { get; set; } = null!;
}

public class FramePlaybackPackage : Entity
{
    public Guid DisplayDeviceId { get; set; }
    public Guid FamilyId { get; set; }
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PackageJson { get; set; } = "{}";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DisplayDevice DisplayDevice { get; set; } = null!;
    public ICollection<FrameComment> Comments { get; set; } = [];
}

public class FrameComment : Entity
{
    public Guid PackageId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public FramePlaybackPackage Package { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public class Friend : Entity
{
    public Guid OwnerUserId { get; set; }
    public Guid? FriendUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool FrameLinked { get; set; }
    public int SharedMemoryCount { get; set; }
    public UserAccount Owner { get; set; } = null!;
}

public class SharedAlbum : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public SharedAlbumType AlbumType { get; set; }
    public bool IsOwn { get; set; } = true;
    public Family Family { get; set; } = null!;
    public ICollection<SharedAlbumAccess> AccessList { get; set; } = [];
    public ICollection<Photo> Photos { get; set; } = [];
}

public class SharedAlbumAccess : Entity
{
    public Guid SharedAlbumId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? FriendId { get; set; }
    public SharedAlbum SharedAlbum { get; set; } = null!;
}

public class UploadSession : Entity
{
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public MediaKind MediaKind { get; set; }
    public string FileName { get; set; } = string.Empty;
    /// <summary>Compressed JPEG content type (image/jpeg).</summary>
    public string ContentType { get; set; } = string.Empty;
    public string? OriginalFileName { get; set; }
    public string? OriginalContentType { get; set; }
    public bool IsLivePhoto { get; set; }
    public string? LivePhotoVideoFileName { get; set; }
    public string? LivePhotoVideoContentType { get; set; }
    /// <summary>Total bytes reserved for quota (still + Live video when applicable).</summary>
    public long FileSizeBytes { get; set; }
    public long OriginalStillFileSizeBytes { get; set; }
    public long LivePhotoVideoFileSizeBytes { get; set; }
    /// <summary>Compressed object key (e.g. abc.jpg).</summary>
    public string S3Key { get; set; } = string.Empty;
    public string? S3KeyFull { get; set; }
    public string? S3KeyThumbnail { get; set; }
    public string? S3KeyLivePhotoVideo { get; set; }
    public DateTime? TakenAt { get; set; }
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Pending;
    public UploadPrivacyScope PrivacyScope { get; set; } = UploadPrivacyScope.Family;
    public bool ActivityParticipantsVisible { get; set; }
    /// <summary>JSON array of family member ids staged until upload complete.</summary>
    public string PrivacyMemberIdsJson { get; set; } = "[]";
    /// <summary>JSON array of friend ids staged until upload complete.</summary>
    public string PrivacyFriendIdsJson { get; set; } = "[]";
    public Guid? SharedAlbumId { get; set; }
    public Guid? ActivityAlbumId { get; set; }
    /// <summary>When true, display key (S3Key) references the same object as S3KeyFull; no separate compressed PUT.</summary>
    public bool CompressedUsesFullOriginal { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid? ResultPhotoId { get; set; }
    public UserAccount User { get; set; } = null!;
    public ActivityAlbum? ActivityAlbum { get; set; }
}

public class ActivityAlbum : Entity, IFamilyScoped
{
    public Guid FamilyId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ActivityAlbumType Type { get; set; }
    public ActivityAlbumStatus Status { get; set; } = ActivityAlbumStatus.InProgress;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Location { get; set; }
    public Guid CreatorUserId { get; set; }
    public Guid? CoverPhotoId { get; set; }
    /// <summary>Default audience for uploads started with this activity (mirrors upload privacyScope).</summary>
    public UploadPrivacyScope PrivacyScope { get; set; } = UploadPrivacyScope.Family;

    public Family Family { get; set; } = null!;
    public UserAccount Creator { get; set; } = null!;
    public Photo? CoverPhoto { get; set; }
    public ICollection<ActivityAgendaItem> AgendaItems { get; set; } = [];
    public ICollection<ActivityAlbumFamilyMember> FamilyMembers { get; set; } = [];
    public ICollection<ActivityAlbumFriend> Friends { get; set; } = [];
    public ICollection<ActivityAlbumPhoto> Photos { get; set; } = [];
}

public class ActivityAgendaItem : Entity
{
    public Guid ActivityAlbumId { get; set; }
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Location { get; set; }
    public int SortOrder { get; set; }
    public ActivityAlbum ActivityAlbum { get; set; } = null!;
}

public class ActivityAlbumFamilyMember : Entity
{
    public Guid ActivityAlbumId { get; set; }
    public Guid FamilyMemberId { get; set; }
    public ActivityAlbum ActivityAlbum { get; set; } = null!;
    public FamilyMember FamilyMember { get; set; } = null!;
}

public class ActivityAlbumFriend : Entity
{
    public Guid ActivityAlbumId { get; set; }
    public Guid? FriendId { get; set; }
    /// <summary>App friend id or resolved Friend.Id string.</summary>
    public string FriendReference { get; set; } = string.Empty;
    public ActivityAlbum ActivityAlbum { get; set; } = null!;
    public Friend? Friend { get; set; }
}

public class ActivityAlbumPhoto : Entity
{
    public Guid ActivityAlbumId { get; set; }
    public Guid PhotoId { get; set; }
    public int SortOrder { get; set; }
    public ActivityAlbum ActivityAlbum { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}

public class AiAnalysisJob : Entity
{
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public AiAnalysisJobStatus Status { get; set; } = AiAnalysisJobStatus.Queued;
    public string PhotoIdsJson { get; set; } = "[]";
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class UserAiSetting : Entity
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public bool Value { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class Notification : Entity
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsRead { get; set; }
    public UserAccount User { get; set; } = null!;
}

public class TodayHighlightCache : Entity
{
    public Guid FamilyId { get; set; }
    public DateOnly CacheDate { get; set; }
    public Guid MemoryId { get; set; }
    public Family Family { get; set; } = null!;
    public Memory Memory { get; set; } = null!;
}

/// <summary>Persisted 今日回憶 photo list — created once per family per calendar day on first API call.</summary>
public class TodayMemoriesCache : Entity
{
    public Guid FamilyId { get; set; }
    public DateOnly CacheDate { get; set; }
    public TodayMemoriesStrategy Strategy { get; set; }
    /// <summary>JSON array of { photoId, reason, yearsAgo, occasionKind }.</summary>
    public string ItemsJson { get; set; } = "[]";
    public Family Family { get; set; } = null!;
}

public class AiChatSession : Entity
{
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public UserAccount User { get; set; } = null!;
    public ICollection<AiChatMessage> Messages { get; set; } = [];
}

public class AiChatMessage : Entity
{
    public Guid SessionId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public Guid? MemoryId { get; set; }
    public Guid? PhotoId { get; set; }
    public string? MatchReasonKeysJson { get; set; }
    /// <summary>JSON array of photo UUIDs shown for multi-photo agent replies.</summary>
    public string? RelatedPhotoIdsJson { get; set; }
    public AiChatSession Session { get; set; } = null!;
}

public class JournalTag : Entity
{
    public Guid OwnerUserId { get; set; }
    public string LabelKey { get; set; } = string.Empty;
    public int ColorArgb { get; set; }
    public bool IsCustom { get; set; } = true;
    public UserAccount Owner { get; set; } = null!;
}
