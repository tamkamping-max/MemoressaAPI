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
    public bool IsActive { get; set; } = true;
    /// <summary>Sum of original (full) photo bytes stored for this user.</summary>
    public long CloudStorageUsedBytes { get; set; }

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
    /// <summary>Private S3 object key for the original media. URLs are generated at read time.</summary>
    public string? S3Key { get; set; }
    /// <summary>Optional separate thumbnail object key. When null, thumbnail URLs use <see cref="S3Key"/>.</summary>
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
    public Guid? SharedAlbumId { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? AiAnalysisJson { get; set; }

    public Family Family { get; set; } = null!;
    public UserAccount UploadedBy { get; set; } = null!;
    public SharedAlbum? SharedAlbum { get; set; }
    public ICollection<PhotoMember> PhotoMembers { get; set; } = [];
    public ICollection<PhotoAiTag> AiTags { get; set; } = [];
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

public class PhotoAiTag : Entity
{
    public Guid PhotoId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public Photo Photo { get; set; } = null!;
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
    public string ContentType { get; set; } = string.Empty;
    /// <summary>Original (full image) size in bytes — used for quota reservation.</summary>
    public long FileSizeBytes { get; set; }
    /// <summary>Compressed object key (logical name e.g. abc.jpg).</summary>
    public string S3Key { get; set; } = string.Empty;
    public string? S3KeyFull { get; set; }
    public string? S3KeyThumbnail { get; set; }
    public DateTime? TakenAt { get; set; }
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Pending;
    public UploadPrivacyScope PrivacyScope { get; set; } = UploadPrivacyScope.Family;
    public Guid? SharedAlbumId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public Guid? ResultPhotoId { get; set; }
    public UserAccount User { get; set; } = null!;
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
