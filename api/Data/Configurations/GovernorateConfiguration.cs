using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
{
    public void Configure(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("governorates");

        builder.ConfigureGuidId();

        builder.Property(governorate => governorate.Code)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(governorate => governorate.Code)
            .IsUnique();

        builder.Property(governorate => governorate.SortOrder)
            .IsRequired();
    }
}
