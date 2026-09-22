using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class FamilyMomentConfiguration : IEntityTypeConfiguration<FamilyMoment>
{
    public void Configure(EntityTypeBuilder<FamilyMoment> builder)
    {
        builder.ToTable("family_moments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.FamilyId);
        builder.HasMany(x => x.MomentPhotos).WithOne(x => x.FamilyMoment).HasForeignKey(x => x.FamilyMomentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.MomentMembers).WithOne(x => x.FamilyMoment).HasForeignKey(x => x.FamilyMomentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FamilyMomentPhotoConfiguration : IEntityTypeConfiguration<FamilyMomentPhoto>
{
    public void Configure(EntityTypeBuilder<FamilyMomentPhoto> builder)
    {
        builder.ToTable("family_moment_photos");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.FamilyMomentId, x.PhotoId }).IsUnique();
    }
}

public class FamilyMomentMemberConfiguration : IEntityTypeConfiguration<FamilyMomentMember>
{
    public void Configure(EntityTypeBuilder<FamilyMomentMember> builder)
    {
        builder.ToTable("family_moment_members");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.FamilyMomentId, x.FamilyMemberId }).IsUnique();
    }
}

public class FriendConfiguration : IEntityTypeConfiguration<Friend>
{
    public void Configure(EntityTypeBuilder<Friend> builder)
    {
        builder.ToTable("friends");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OwnerUserId);
        builder.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SharedAlbumConfiguration : IEntityTypeConfiguration<SharedAlbum>
{
    public void Configure(EntityTypeBuilder<SharedAlbum> builder)
    {
        builder.ToTable("shared_albums");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.FamilyId, x.ExternalId }).IsUnique();
        builder.HasMany(x => x.AccessList).WithOne(x => x.SharedAlbum).HasForeignKey(x => x.SharedAlbumId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SharedAlbumAccessConfiguration : IEntityTypeConfiguration<SharedAlbumAccess>
{
    public void Configure(EntityTypeBuilder<SharedAlbumAccess> builder)
    {
        builder.ToTable("shared_album_access");
        builder.HasKey(x => x.Id);
    }
}

public class UploadSessionConfiguration : IEntityTypeConfiguration<UploadSession>
{
    public void Configure(EntityTypeBuilder<UploadSession> builder)
    {
        builder.ToTable("upload_sessions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.FamilyId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiAnalysisJobConfiguration : IEntityTypeConfiguration<AiAnalysisJob>
{
    public void Configure(EntityTypeBuilder<AiAnalysisJob> builder)
    {
        builder.ToTable("ai_analysis_jobs");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.FamilyId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserAiSettingConfiguration : IEntityTypeConfiguration<UserAiSetting>
{
    public void Configure(EntityTypeBuilder<UserAiSetting> builder)
    {
        builder.ToTable("user_ai_settings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
{
    public void Configure(EntityTypeBuilder<AiChatSession> builder)
    {
        builder.ToTable("ai_chat_sessions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.UpdatedAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Messages).WithOne(x => x.Session).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("ai_chat_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.SessionId, x.CreatedAt });
    }
}
