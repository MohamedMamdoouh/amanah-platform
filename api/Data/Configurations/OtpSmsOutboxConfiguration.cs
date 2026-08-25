using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class OtpSmsOutboxConfiguration : IEntityTypeConfiguration<OtpSmsOutboxMessage>
{
    public void Configure(EntityTypeBuilder<OtpSmsOutboxMessage> builder)
    {
        builder.ToTable("otp_sms_outbox");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.Phone)
            .HasMaxLength(16)
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

        builder.HasIndex(message => new { message.Phone, message.Status, message.ProcessedAt });

        builder.HasIndex(message => new { message.Status, message.CreatedAt });

        builder.HasOne(message => message.OtpCode)
            .WithMany()
            .HasForeignKey(message => message.OtpCodeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
