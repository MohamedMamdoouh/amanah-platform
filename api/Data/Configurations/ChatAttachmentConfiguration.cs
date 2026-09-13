using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ChatAttachmentConfiguration : IEntityTypeConfiguration<ChatAttachment>
{
    public void Configure(EntityTypeBuilder<ChatAttachment> builder)
    {
        builder.ToTable("chat_attachments");

        builder.ConfigureGuidId();

        builder.Property(attachment => attachment.StorageKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attachment => attachment.ThumbnailStorageKey)
            .HasMaxLength(200);

        builder.Property(attachment => attachment.CreatedAt)
            .IsRequired();

        builder.HasOne(attachment => attachment.ChatThread)
            .WithMany()
            .HasForeignKey(attachment => attachment.ChatThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(attachment => attachment.Uploader)
            .WithMany()
            .HasForeignKey(attachment => attachment.UploaderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(attachment => attachment.Message)
            .WithOne(message => message.Attachment)
            .HasForeignKey<ChatAttachment>(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(attachment => attachment.MessageId)
            .IsUnique()
            .HasFilter("\"MessageId\" IS NOT NULL");
    }
}
