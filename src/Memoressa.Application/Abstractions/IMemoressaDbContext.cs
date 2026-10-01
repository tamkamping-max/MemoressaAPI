using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Memoressa.Application.Abstractions;

public interface IMemoressaDbContext
{
    DatabaseFacade Database { get; }

    DbSet<UserAccount> UserAccounts { get; }
    DbSet<UserOAuthLink> UserOAuthLinks { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<PasswordResetCode> PasswordResetCodes { get; }
    DbSet<EmailVerificationCode> EmailVerificationCodes { get; }
    DbSet<EmailChangeCode> EmailChangeCodes { get; }
    DbSet<FriendInvite> FriendInvites { get; }
    DbSet<Family> Families { get; }
    DbSet<FamilyMembership> FamilyMemberships { get; }
    DbSet<FamilyMember> FamilyMembers { get; }
    DbSet<Photo> Photos { get; }
    DbSet<PhotoMember> PhotoMembers { get; }
    DbSet<PhotoFriend> PhotoFriends { get; }
    DbSet<PhotoAiTag> PhotoAiTags { get; }
    DbSet<PhotoUserTag> PhotoUserTags { get; }
    DbSet<UserPhotoTagLibraryEntry> UserPhotoTagLibraryEntries { get; }
    DbSet<PhotoAlbum> PhotoAlbums { get; }
    DbSet<PhotoAlbumPhoto> PhotoAlbumPhotos { get; }
    DbSet<PhotoAlbumUserTag> PhotoAlbumUserTags { get; }
    DbSet<PhotoAlbumMember> PhotoAlbumMembers { get; }
    DbSet<PhotoAlbumComment> PhotoAlbumComments { get; }
    DbSet<PhotoComment> PhotoComments { get; }
    DbSet<PhotoAiInference> PhotoAiInferences { get; }
    DbSet<Memory> Memories { get; }
    DbSet<MemoryPhoto> MemoryPhotos { get; }
    DbSet<MemoryVideo> MemoryVideos { get; }
    DbSet<MemoryMember> MemoryMembers { get; }
    DbSet<FamilyMoment> FamilyMoments { get; }
    DbSet<FamilyMomentPhoto> FamilyMomentPhotos { get; }
    DbSet<FamilyMomentMember> FamilyMomentMembers { get; }
    DbSet<DisplayDevice> DisplayDevices { get; }
    DbSet<FrameCommand> FrameCommands { get; }
    DbSet<FramePlaybackPackage> FramePlaybackPackages { get; }
    DbSet<FrameComment> FrameComments { get; }
    DbSet<Friend> Friends { get; }
    DbSet<SharedAlbum> SharedAlbums { get; }
    DbSet<SharedAlbumAccess> SharedAlbumAccesses { get; }
    DbSet<UploadSession> UploadSessions { get; }
    DbSet<AiAnalysisJob> AiAnalysisJobs { get; }
    DbSet<UserAiSetting> UserAiSettings { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<TodayHighlightCache> TodayHighlightCaches { get; }
    DbSet<TodayMemoriesCache> TodayMemoriesCaches { get; }
    DbSet<ActivityAlbum> ActivityAlbums { get; }
    DbSet<ActivityAgendaItem> ActivityAgendaItems { get; }
    DbSet<ActivityAlbumFamilyMember> ActivityAlbumFamilyMembers { get; }
    DbSet<ActivityAlbumFriend> ActivityAlbumFriends { get; }
    DbSet<ActivityAlbumPhoto> ActivityAlbumPhotos { get; }
    DbSet<AiChatSession> AiChatSessions { get; }
    DbSet<AiChatMessage> AiChatMessages { get; }
    DbSet<JournalTag> JournalTags { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
