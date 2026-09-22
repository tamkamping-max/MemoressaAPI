using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Infrastructure.Data;

public class MemoressaDbContext : DbContext, IMemoressaDbContext
{
    public MemoressaDbContext(DbContextOptions<MemoressaDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<UserOAuthLink> UserOAuthLinks => Set<UserOAuthLink>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Family> Families => Set<Family>();
    public DbSet<FamilyMembership> FamilyMemberships => Set<FamilyMembership>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<PhotoMember> PhotoMembers => Set<PhotoMember>();
    public DbSet<PhotoAiTag> PhotoAiTags => Set<PhotoAiTag>();
    public DbSet<PhotoAiInference> PhotoAiInferences => Set<PhotoAiInference>();
    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<MemoryPhoto> MemoryPhotos => Set<MemoryPhoto>();
    public DbSet<MemoryVideo> MemoryVideos => Set<MemoryVideo>();
    public DbSet<MemoryMember> MemoryMembers => Set<MemoryMember>();
    public DbSet<FamilyMoment> FamilyMoments => Set<FamilyMoment>();
    public DbSet<FamilyMomentPhoto> FamilyMomentPhotos => Set<FamilyMomentPhoto>();
    public DbSet<FamilyMomentMember> FamilyMomentMembers => Set<FamilyMomentMember>();
    public DbSet<DisplayDevice> DisplayDevices => Set<DisplayDevice>();
    public DbSet<FrameCommand> FrameCommands => Set<FrameCommand>();
    public DbSet<FramePlaybackPackage> FramePlaybackPackages => Set<FramePlaybackPackage>();
    public DbSet<FrameComment> FrameComments => Set<FrameComment>();
    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<SharedAlbum> SharedAlbums => Set<SharedAlbum>();
    public DbSet<SharedAlbumAccess> SharedAlbumAccesses => Set<SharedAlbumAccess>();
    public DbSet<UploadSession> UploadSessions => Set<UploadSession>();
    public DbSet<AiAnalysisJob> AiAnalysisJobs => Set<AiAnalysisJob>();
    public DbSet<UserAiSetting> UserAiSettings => Set<UserAiSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TodayHighlightCache> TodayHighlightCaches => Set<TodayHighlightCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MemoressaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Domain.Common.Entity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
