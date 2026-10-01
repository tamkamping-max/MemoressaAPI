using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class EmailVerificationCodeConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
{
    public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
    {
        builder.ToTable("email_verification_codes");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailChangeCodeConfiguration : IEntityTypeConfiguration<EmailChangeCode>
{
    public void Configure(EntityTypeBuilder<EmailChangeCode> builder)
    {
        builder.ToTable("email_change_codes");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.Property(x => x.NewEmail).HasMaxLength(320).IsRequired();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FriendInviteConfiguration : IEntityTypeConfiguration<FriendInvite>
{
    public void Configure(EntityTypeBuilder<FriendInvite> builder)
    {
        builder.ToTable("friend_invites");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InviteeEmail).HasMaxLength(320).IsRequired();
        builder.HasIndex(x => x.InviterUserId);
        builder.HasIndex(x => x.InviteeUserId);
        builder.HasIndex(x => x.InviteeEmail);
        builder.HasOne(x => x.Inviter).WithMany().HasForeignKey(x => x.InviterUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Invitee).WithMany().HasForeignKey(x => x.InviteeUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
