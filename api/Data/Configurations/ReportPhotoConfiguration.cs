using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ReportPhotoConfiguration : IEntityTypeConfiguration<ReportPhoto>
{
    public void Configure(EntityTypeBuilder<ReportPhoto> builder)
    {
        builder.ToTable("report_photos");

        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.Id)
            .ValueGeneratedNever();

        builder.Property(photo => photo.StorageKey)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(photo => photo.ContentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(photo => photo.ThumbnailStorageKey)
            .HasMaxLength(200);

        builder.HasOne(photo => photo.Report)
            .WithMany(report => report.Photos)
            .HasForeignKey(photo => photo.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
