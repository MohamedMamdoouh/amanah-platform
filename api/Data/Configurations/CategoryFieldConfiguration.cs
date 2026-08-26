using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class CategoryFieldConfiguration : IEntityTypeConfiguration<CategoryField>
{
    public void Configure(EntityTypeBuilder<CategoryField> builder)
    {
        builder.ToTable("category_fields");

        builder.HasKey(field => field.Id);

        builder.Property(field => field.Id)
            .ValueGeneratedNever();

        builder.Property(field => field.FieldKey)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(field => field.Value)
            .HasMaxLength(80)
            .IsRequired();

        builder.HasIndex(field => new { field.ReportId, field.FieldKey })
            .IsUnique();

        builder.HasOne(field => field.Report)
            .WithMany(report => report.CategoryFields)
            .HasForeignKey(field => field.ReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
