using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class StorageDeletionOutboxConfiguration : IEntityTypeConfiguration<StorageDeletionOutboxMessage>
{
    public void Configure(EntityTypeBuilder<StorageDeletionOutboxMessage> builder)
    {
        builder.ToTable("storage_deletion_outbox");

        builder.ConfigureGuidId();

        builder.Property(message => message.StorageKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.Status)
            .HasMaxLength(16)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(message => message.Source)
            .HasMaxLength(32)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        builder.Property(message => message.AttemptCount)
            .HasDefaultValue(0);

        builder.HasIndex(message => new { message.Status, message.CreatedAt });

        builder.HasIndex(message => message.StorageKey)
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
