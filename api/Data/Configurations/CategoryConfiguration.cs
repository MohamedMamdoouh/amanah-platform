using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .ValueGeneratedNever();

        builder.Property(category => category.Code)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(category => category.Code)
            .IsUnique();

        builder.Property(category => category.SortOrder)
            .IsRequired();

        builder.Property(category => category.PhotosPrivate)
            .HasDefaultValue(false);

        builder.Property(category => category.Active)
            .HasDefaultValue(true);
    }
}
