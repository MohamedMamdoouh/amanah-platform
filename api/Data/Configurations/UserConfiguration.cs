using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .ValueGeneratedNever();

        builder.Property(user => user.NormalizedPhone)
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(user => user.NormalizedPhone)
            .IsUnique();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(40);

        builder.Property(user => user.Role)
            .HasMaxLength(10)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(user => user.IsBanned)
            .HasDefaultValue(false);

        builder.Property(user => user.BanReason);

        builder.Property(user => user.CreatedAt)
            .IsRequired();
    }
}
