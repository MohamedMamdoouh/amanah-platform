using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ChatThreadConfiguration : IEntityTypeConfiguration<ChatThread>
{
    public void Configure(EntityTypeBuilder<ChatThread> builder)
    {
        builder.ToTable("chat_threads");

        builder.HasKey(thread => thread.Id);

        builder.Property(thread => thread.Id)
            .ValueGeneratedNever();

        builder.Property(thread => thread.CreatedAt)
            .IsRequired();

        builder.HasIndex(thread => thread.ClaimId)
            .IsUnique();

        builder.HasOne(thread => thread.Claim)
            .WithOne(claim => claim.ChatThread)
            .HasForeignKey<ChatThread>(thread => thread.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
