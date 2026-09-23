using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class OtpEmailOutboxConfiguration : IEntityTypeConfiguration<OtpEmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<OtpEmailOutboxMessage> builder)
    {
        builder.ToTable("otp_email_outbox");

        builder.ConfigureGuidId();

        builder.Property(message => message.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(message => message.ProtectedPayload)
            .IsRequired();

        builder.Property(message => message.Status)
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        builder.Property(message => message.AttemptCount)
            .HasDefaultValue(0);

        builder.HasIndex(message => new { message.Email, message.Status, message.ProcessedAt });

        builder.HasIndex(message => new { message.Status, message.CreatedAt });

        builder.HasOne(message => message.OtpCode)
            .WithMany()
            .HasForeignKey(message => message.OtpCodeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
