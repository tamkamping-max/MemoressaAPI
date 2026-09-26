using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Memoressa.Infrastructure.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Locale).HasMaxLength(16).HasDefaultValue("en");
        builder.Property(x => x.CloudStorageUsedBytes).HasDefaultValue(0L);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasMany(x => x.OAuthLinks).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.RefreshTokens).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.FamilyMemberships).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AiSettings).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.Token).IsUnique();
    }
}

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.Token).IsUnique();
    }
}

public class PasswordResetCodeConfiguration : IEntityTypeConfiguration<PasswordResetCode>
{
    public void Configure(EntityTypeBuilder<PasswordResetCode> builder)
    {
        builder.ToTable("password_reset_codes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserOAuthLinkConfiguration : IEntityTypeConfiguration<UserOAuthLink>
{
    public void Configure(EntityTypeBuilder<UserOAuthLink> builder)
    {
        builder.ToTable("user_oauth_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderUserId).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
    }
}
