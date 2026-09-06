using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class AdminAlertEmailOutboxConfiguration : IEntityTypeConfiguration<AdminAlertEmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<AdminAlertEmailOutboxMessage> builder)
    {
        builder.ToTable("admin_alert_email_outbox");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.ReportType)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(message => message.CategoryCode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(message => message.Status)
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        builder.Property(message => message.AttemptCount)
            .HasDefaultValue(0);

        builder.Property(message => message.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(message => new { message.Status, message.CreatedAt });

        builder.HasIndex(message => message.ReportId);

        builder.HasOne(message => message.Report)
            .WithMany()
            .HasForeignKey(message => message.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
