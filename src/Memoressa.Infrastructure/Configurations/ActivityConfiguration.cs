using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class ActivityAlbumConfiguration : IEntityTypeConfiguration<ActivityAlbum>
{
    public void Configure(EntityTypeBuilder<ActivityAlbum> builder)
    {
        builder.ToTable("activity_albums");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.FamilyId, x.ExternalId }).IsUnique();
        builder.HasIndex(x => new { x.FamilyId, x.Status });
        builder.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CoverPhoto).WithMany().HasForeignKey(x => x.CoverPhotoId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ActivityAgendaItemConfiguration : IEntityTypeConfiguration<ActivityAgendaItem>
{
    public void Configure(EntityTypeBuilder<ActivityAgendaItem> builder)
    {
        builder.ToTable("activity_agenda_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.ActivityAlbum).WithMany(x => x.AgendaItems).HasForeignKey(x => x.ActivityAlbumId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ActivityAlbumFamilyMemberConfiguration : IEntityTypeConfiguration<ActivityAlbumFamilyMember>
{
    public void Configure(EntityTypeBuilder<ActivityAlbumFamilyMember> builder)
    {
        builder.ToTable("activity_album_family_members");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ActivityAlbumId, x.FamilyMemberId }).IsUnique();
    }
}

public class ActivityAlbumFriendConfiguration : IEntityTypeConfiguration<ActivityAlbumFriend>
{
    public void Configure(EntityTypeBuilder<ActivityAlbumFriend> builder)
    {
        builder.ToTable("activity_album_friends");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FriendReference).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.ActivityAlbumId, x.FriendReference }).IsUnique();
        builder.HasOne(x => x.Friend).WithMany().HasForeignKey(x => x.FriendId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ActivityAlbumPhotoConfiguration : IEntityTypeConfiguration<ActivityAlbumPhoto>
{
    public void Configure(EntityTypeBuilder<ActivityAlbumPhoto> builder)
    {
        builder.ToTable("activity_album_photos");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ActivityAlbumId, x.PhotoId }).IsUnique();
        builder.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
    }
}
