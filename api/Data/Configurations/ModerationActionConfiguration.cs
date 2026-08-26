using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> builder)
    {
        builder.ToTable("moderation_actions");

        builder.HasKey(action => action.Id);

        builder.Property(action => action.Id)
            .ValueGeneratedNever();

        builder.Property(action => action.Decision)
            .HasMaxLength(15)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(action => action.ReasonCode)
            .HasMaxLength(40);

        builder.Property(action => action.Note)
            .HasMaxLength(500);

        builder.Property(action => action.CreatedAt)
            .IsRequired();

        builder.HasOne(action => action.Report)
            .WithMany(report => report.ModerationActions)
            .HasForeignKey(action => action.ReportId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(action => action.Admin)
            .WithMany()
            .HasForeignKey(action => action.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
