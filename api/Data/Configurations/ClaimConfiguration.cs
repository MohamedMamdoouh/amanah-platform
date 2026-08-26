using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");

        builder.HasKey(claim => claim.Id);

        builder.Property(claim => claim.Id)
            .ValueGeneratedNever();

        builder.Property(claim => claim.Status)
            .HasMaxLength(15)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(claim => claim.SubmittedAnswer)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(claim => claim.PhotoStorageKey)
            .HasMaxLength(200);

        builder.Property(claim => claim.DecisionReason)
            .HasMaxLength(500);

        builder.Property(claim => claim.ReviewerDecision)
            .HasMaxLength(20);

        builder.Property(claim => claim.SubmittedAt)
            .IsRequired();

        builder.HasOne(claim => claim.Report)
            .WithMany(report => report.Claims)
            .HasForeignKey(claim => claim.ReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(claim => claim.Claimant)
            .WithMany()
            .HasForeignKey(claim => claim.ClaimantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(claim => claim.CancelledByUser)
            .WithMany()
            .HasForeignKey(claim => claim.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
