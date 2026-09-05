using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .ValueGeneratedNever();

        builder.Property(report => report.Type)
            .HasMaxLength(10)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(report => report.Title)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(report => report.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(report => report.AreaText)
            .HasMaxLength(120);

        builder.Property(report => report.HeldLocation)
            .HasMaxLength(120);

        builder.Property(report => report.Status)
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(report => report.HiddenDetail)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(report => report.WithdrawalReason)
            .HasMaxLength(500);

        builder.Property(report => report.NormalizedSearchText);

        builder.Property(report => report.CreatedAt)
            .IsRequired();

        builder.Property(report => report.UpdatedAt)
            .IsRequired();

        builder.HasOne(report => report.Reporter)
            .WithMany()
            .HasForeignKey(report => report.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.Category)
            .WithMany(category => category.Reports)
            .HasForeignKey(report => report.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.Governorate)
            .WithMany(governorate => governorate.Reports)
            .HasForeignKey(report => report.GovernorateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(report => report.Resolution)
            .WithOne(resolution => resolution.Report)
            .HasForeignKey<Resolution>(resolution => resolution.ReportId);

        builder.HasIndex(report => new { report.Status, report.CreatedAt });
    }
}
