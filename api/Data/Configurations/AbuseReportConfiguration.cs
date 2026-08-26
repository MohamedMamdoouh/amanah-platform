using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class AbuseReportConfiguration : IEntityTypeConfiguration<AbuseReport>
{
    public void Configure(EntityTypeBuilder<AbuseReport> builder)
    {
        builder.ToTable("abuse_reports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .ValueGeneratedNever();

        builder.Property(report => report.Reason)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(report => report.Note)
            .HasMaxLength(500);

        builder.Property(report => report.Status)
            .HasMaxLength(10)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(report => report.ResolutionOutcome)
            .HasMaxLength(40);

        builder.Property(report => report.CreatedAt)
            .IsRequired();

        builder.HasOne(report => report.Reporter)
            .WithMany()
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.Report)
            .WithMany(r => r.AbuseReports)
            .HasForeignKey(report => report.ReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.ResolvedByUser)
            .WithMany()
            .HasForeignKey(report => report.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
