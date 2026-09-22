using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class DisplayDeviceConfiguration : IEntityTypeConfiguration<DisplayDevice>
{
    public void Configure(EntityTypeBuilder<DisplayDevice> builder)
    {
        builder.ToTable("display_devices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.QrCode).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.QrCode).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasOne(x => x.CurrentMemory).WithMany().HasForeignKey(x => x.CurrentMemoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Commands).WithOne(x => x.DisplayDevice).HasForeignKey(x => x.DisplayDeviceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.PlaybackPackages).WithOne(x => x.DisplayDevice).HasForeignKey(x => x.DisplayDeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FrameCommandConfiguration : IEntityTypeConfiguration<FrameCommand>
{
    public void Configure(EntityTypeBuilder<FrameCommand> builder)
    {
        builder.ToTable("frame_commands");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DisplayDeviceId, x.Status });
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb");
    }
}

public class FramePlaybackPackageConfiguration : IEntityTypeConfiguration<FramePlaybackPackage>
{
    public void Configure(EntityTypeBuilder<FramePlaybackPackage> builder)
    {
        builder.ToTable("frame_playback_packages");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.DisplayDeviceId);
        builder.Property(x => x.ExternalId).HasMaxLength(128);
        builder.HasIndex(x => new { x.DisplayDeviceId, x.ExternalId }).IsUnique();
        builder.Property(x => x.PackageJson).HasColumnType("jsonb");
        builder.HasMany(x => x.Comments).WithOne(x => x.Package).HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FrameCommentConfiguration : IEntityTypeConfiguration<FrameComment>
{
    public void Configure(EntityTypeBuilder<FrameComment> builder)
    {
        builder.ToTable("frame_comments");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
