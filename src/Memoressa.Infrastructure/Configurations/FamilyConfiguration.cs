using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> builder)
    {
        builder.ToTable("families");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.OwnerUserId);
        builder.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Memberships).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Members).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Photos).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Memories).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Moments).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.DisplayDevices).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.SharedAlbums).WithOne(x => x.Family).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FamilyMembershipConfiguration : IEntityTypeConfiguration<FamilyMembership>
{
    public void Configure(EntityTypeBuilder<FamilyMembership> builder)
    {
        builder.ToTable("family_memberships");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.FamilyId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.FamilyId);
    }
}

public class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> builder)
    {
        builder.ToTable("family_members");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.FamilyId);
        builder.HasMany(x => x.PhotoMembers).WithOne(x => x.FamilyMember).HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.MemoryMembers).WithOne(x => x.FamilyMember).HasForeignKey(x => x.FamilyMemberId).OnDelete(DeleteBehavior.Cascade);
    }
}
