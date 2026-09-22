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
    DbSet<Family> Families { get; }
    DbSet<FamilyMembership> FamilyMemberships { get; }
    DbSet<FamilyMember> FamilyMembers { get; }
    DbSet<Photo> Photos { get; }
    DbSet<PhotoMember> PhotoMembers { get; }
    DbSet<PhotoAiTag> PhotoAiTags { get; }
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
    DbSet<AiChatSession> AiChatSessions { get; }
    DbSet<AiChatMessage> AiChatMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
