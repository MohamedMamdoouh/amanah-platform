using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.Body)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(message => message.AttachmentStorageKey)
            .HasMaxLength(200);

        builder.Property(message => message.SentAt)
            .IsRequired();

        builder.HasOne(message => message.ChatThread)
            .WithMany(thread => thread.Messages)
            .HasForeignKey(message => message.ChatThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
