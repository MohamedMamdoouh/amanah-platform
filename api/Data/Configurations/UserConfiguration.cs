using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.ConfigureGuidId();

        builder.Property(user => user.NormalizedPhone)
            .HasMaxLength(16);

        builder.Property(user => user.NormalizedEmail)
            .HasMaxLength(254);

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(user => user.NormalizedPhone)
            .IsUnique();

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_users_at_least_one_identifier",
            "\"NormalizedPhone\" IS NOT NULL OR \"NormalizedEmail\" IS NOT NULL"));

        builder.Property(user => user.DisplayName)
            .HasMaxLength(40);

        builder.Property(user => user.Role)
            .HasMaxLength(10)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(user => user.IsBanned)
            .HasDefaultValue(false);

        builder.Property(user => user.BanReason);

        builder.Property(user => user.BannedAt);

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.DeactivatedAt);
    }
}
