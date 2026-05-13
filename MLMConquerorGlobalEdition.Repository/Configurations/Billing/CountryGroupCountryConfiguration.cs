using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class CountryGroupCountryConfiguration : IEntityTypeConfiguration<CountryGroupCountry>
{
    public void Configure(EntityTypeBuilder<CountryGroupCountry> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsoCountryCode).IsRequired().HasMaxLength(3);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.CountryGroupId, x.IsoCountryCode }).IsUnique();
    }
}
