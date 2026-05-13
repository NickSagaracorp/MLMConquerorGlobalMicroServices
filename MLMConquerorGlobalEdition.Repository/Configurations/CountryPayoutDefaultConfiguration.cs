using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// One row per country enforced via a unique index on CountryIso2 (length-2
/// alpha) so the signup pipeline can do a single deterministic lookup.
/// Inactive rows are kept for audit but skipped by the signup query.
/// </summary>
public class CountryPayoutDefaultConfiguration : IEntityTypeConfiguration<CountryPayoutDefault>
{
    public void Configure(EntityTypeBuilder<CountryPayoutDefault> builder)
    {
        builder.ToTable("CountryPayoutDefaults");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CountryIso2).IsRequired().HasMaxLength(2);
        builder.HasIndex(x => x.CountryIso2).IsUnique();

        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);
    }
}
