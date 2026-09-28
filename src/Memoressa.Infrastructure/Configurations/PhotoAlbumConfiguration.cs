using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class PhotoAlbumConfiguration : IEntityTypeConfiguration<PhotoAlbum>
{
    public void Configure(EntityTypeBuilder<PhotoAlbum> builder)
    {
        builder.ToTable("photo_albums");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PhotoSetFingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => new { x.FamilyId, x.PhotoSetFingerprint }).IsUnique();
        builder.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.AlbumPhotos).WithOne(x => x.PhotoAlbum).HasForeignKey(x => x.PhotoAlbumId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.UserTags).WithOne(x => x.PhotoAlbum).HasForeignKey(x => x.PhotoAlbumId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AlbumMembers).WithOne(x => x.PhotoAlbum).HasForeignKey(x => x.PhotoAlbumId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Comments).WithOne(x => x.PhotoAlbum).HasForeignKey(x => x.PhotoAlbumId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PhotoAlbumPhotoConfiguration : IEntityTypeConfiguration<PhotoAlbumPhoto>
{
    public void Configure(EntityTypeBuilder<PhotoAlbumPhoto> builder)
    {
        builder.ToTable("photo_album_photos");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PhotoAlbumId, x.PhotoId }).IsUnique();
        builder.HasIndex(x => x.PhotoId);
        builder.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PhotoAlbumUserTagConfiguration : IEntityTypeConfiguration<PhotoAlbumUserTag>
{
    public void Configure(EntityTypeBuilder<PhotoAlbumUserTag> builder)
    {
        builder.ToTable("photo_album_user_tags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tag).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.PhotoAlbumId, x.Tag }).IsUnique();
    }
}

public class PhotoAlbumMemberConfiguration : IEntityTypeConfiguration<PhotoAlbumMember>
{
    public void Configure(EntityTypeBuilder<PhotoAlbumMember> builder)
    {
        builder.ToTable("photo_album_members");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PhotoAlbumId, x.FamilyMemberId }).IsUnique();
        builder.HasOne(x => x.FamilyMember).WithMany().HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PhotoAlbumCommentConfiguration : IEntityTypeConfiguration<PhotoAlbumComment>
{
    public void Configure(EntityTypeBuilder<PhotoAlbumComment> builder)
    {
        builder.ToTable("photo_album_comments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).IsRequired();
        builder.HasIndex(x => x.PhotoAlbumId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
