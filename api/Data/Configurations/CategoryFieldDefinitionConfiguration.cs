using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class CategoryFieldDefinitionConfiguration : IEntityTypeConfiguration<CategoryFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CategoryFieldDefinition> builder)
    {
        builder.ToTable("category_field_definitions");

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.Id)
            .ValueGeneratedNever();

        builder.Property(definition => definition.FieldKey)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(definition => new { definition.CategoryId, definition.FieldKey })
            .IsUnique();

        builder.Property(definition => definition.Type)
            .HasMaxLength(10)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(definition => definition.TextFormat)
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(definition => definition.Required)
            .HasDefaultValue(true);

        builder.HasOne(definition => definition.Category)
            .WithMany(category => category.FieldDefinitions)
            .HasForeignKey(definition => definition.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
