using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_codes");

        builder.ConfigureGuidId();

        builder.Property(otpCode => otpCode.Destination)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(otpCode => otpCode.Channel)
            .HasMaxLength(8)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(otpCode => new { otpCode.Destination, otpCode.Channel })
            .IsUnique();

        builder.Property(otpCode => otpCode.CodeHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(otpCode => otpCode.ExpiresAt)
            .IsRequired();

        builder.Property(otpCode => otpCode.AttemptCount)
            .HasDefaultValue(0);

        builder.Property(otpCode => otpCode.CreatedAt)
            .IsRequired();
    }
}
