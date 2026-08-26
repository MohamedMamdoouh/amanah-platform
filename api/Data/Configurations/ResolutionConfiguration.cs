using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amanah.Api.Data.Configurations;

public sealed class ResolutionConfiguration : IEntityTypeConfiguration<Resolution>
{
    public void Configure(EntityTypeBuilder<Resolution> builder)
    {
        builder.ToTable("resolutions");

        builder.HasKey(resolution => resolution.Id);

        builder.Property(resolution => resolution.Id)
            .ValueGeneratedNever();

        builder.HasIndex(resolution => resolution.ReportId)
            .IsUnique();
    }
}
