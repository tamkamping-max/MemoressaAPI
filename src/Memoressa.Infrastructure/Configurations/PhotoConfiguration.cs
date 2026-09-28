using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("photos");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.TakenAt);
        builder.HasIndex(x => x.UploadedByUserId);
        builder.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SharedAlbum).WithMany(x => x.Photos).HasForeignKey(x => x.SharedAlbumId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.PhotoMembers).WithOne(x => x.Photo).HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AiTags).WithOne(x => x.Photo).HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.UserTags).WithOne(x => x.Photo).HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Comments).WithOne(x => x.Photo).HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AiInferences).WithOne(x => x.Photo).HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.AiVisionCostUsd).HasPrecision(18, 6);
        builder.Property(x => x.AiVisionModel).HasMaxLength(64);
    }
}

public class PhotoMemberConfiguration : IEntityTypeConfiguration<PhotoMember>
{
    public void Configure(EntityTypeBuilder<PhotoMember> builder)
    {
        builder.ToTable("photo_members");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PhotoId, x.FamilyMemberId }).IsUnique();
    }
}

public class PhotoAiTagConfiguration : IEntityTypeConfiguration<PhotoAiTag>
{
    public void Configure(EntityTypeBuilder<PhotoAiTag> builder)
    {
        builder.ToTable("photo_ai_tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tag).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Tag);
    }
}

public class PhotoUserTagConfiguration : IEntityTypeConfiguration<PhotoUserTag>
{
    public void Configure(EntityTypeBuilder<PhotoUserTag> builder)
    {
        builder.ToTable("photo_user_tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tag).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.PhotoId, x.Tag }).IsUnique();
        builder.HasIndex(x => x.Tag);
    }
}

public class UserPhotoTagLibraryEntryConfiguration : IEntityTypeConfiguration<UserPhotoTagLibraryEntry>
{
    public void Configure(EntityTypeBuilder<UserPhotoTagLibraryEntry> builder)
    {
        builder.ToTable("user_photo_tag_library");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tag).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.Tag }).IsUnique();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PhotoCommentConfiguration : IEntityTypeConfiguration<PhotoComment>
{
    public void Configure(EntityTypeBuilder<PhotoComment> builder)
    {
        builder.ToTable("photo_comments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).IsRequired();
        builder.HasIndex(x => x.PhotoId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PhotoAiInferenceConfiguration : IEntityTypeConfiguration<PhotoAiInference>
{
    public void Configure(EntityTypeBuilder<PhotoAiInference> builder)
    {
        builder.ToTable("photo_ai_inferences");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SuggestedMember).WithMany().HasForeignKey(x => x.SuggestedMemberId).OnDelete(DeleteBehavior.SetNull);
    }
}
