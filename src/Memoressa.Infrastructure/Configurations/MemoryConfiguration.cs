using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class MemoryConfiguration : IEntityTypeConfiguration<Memory>
{
    public void Configure(EntityTypeBuilder<Memory> builder)
    {
        builder.ToTable("memories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.StartDate);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.MemoryPhotos).WithOne(x => x.Memory).HasForeignKey(x => x.MemoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.MemoryVideos).WithOne(x => x.Memory).HasForeignKey(x => x.MemoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.MemoryMembers).WithOne(x => x.Memory).HasForeignKey(x => x.MemoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MemoryPhotoConfiguration : IEntityTypeConfiguration<MemoryPhoto>
{
    public void Configure(EntityTypeBuilder<MemoryPhoto> builder)
    {
        builder.ToTable("memory_photos");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MemoryId, x.PhotoId }).IsUnique();
        builder.HasOne(x => x.Photo).WithMany(x => x.MemoryPhotos).HasForeignKey(x => x.PhotoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MemoryVideoConfiguration : IEntityTypeConfiguration<MemoryVideo>
{
    public void Configure(EntityTypeBuilder<MemoryVideo> builder)
    {
        builder.ToTable("memory_videos");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MemoryId, x.PhotoId }).IsUnique();
    }
}

public class MemoryMemberConfiguration : IEntityTypeConfiguration<MemoryMember>
{
    public void Configure(EntityTypeBuilder<MemoryMember> builder)
    {
        builder.ToTable("memory_members");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MemoryId, x.FamilyMemberId }).IsUnique();
    }
}

public class TodayHighlightCacheConfiguration : IEntityTypeConfiguration<TodayHighlightCache>
{
    public void Configure(EntityTypeBuilder<TodayHighlightCache> builder)
    {
        builder.ToTable("today_highlight_cache");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.FamilyId, x.CacheDate }).IsUnique();
        builder.HasOne(x => x.Memory).WithMany().HasForeignKey(x => x.MemoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TodayMemoriesCacheConfiguration : IEntityTypeConfiguration<TodayMemoriesCache>
{
    public void Configure(EntityTypeBuilder<TodayMemoriesCache> builder)
    {
        builder.ToTable("today_memories_cache");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.FamilyId, x.CacheDate }).IsUnique();
        builder.Property(x => x.ItemsJson).IsRequired();
        builder.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
    }
}
