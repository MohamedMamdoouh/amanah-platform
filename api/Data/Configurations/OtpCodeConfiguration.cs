using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_codes");

        builder.HasKey(otpCode => otpCode.Id);

        builder.Property(otpCode => otpCode.Id)
            .ValueGeneratedNever();

        builder.Property(otpCode => otpCode.Phone)
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(otpCode => otpCode.Phone);

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
