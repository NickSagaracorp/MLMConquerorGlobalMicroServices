using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Iso2).IsRequired().HasMaxLength(2).IsFixedLength();
        builder.Property(x => x.Iso3).IsRequired().HasMaxLength(3).IsFixedLength();
        builder.Property(x => x.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NameNative).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DefaultLanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(x => x.FlagEmoji).IsRequired().HasMaxLength(10);
        builder.Property(x => x.PhoneCode).HasMaxLength(10);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.Iso2).IsUnique();
        builder.HasIndex(x => x.Iso3).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.RegionId);
    }
}
