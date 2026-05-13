using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class CountryGroupConfiguration : IEntityTypeConfiguration<CountryGroup>
{
    public void Configure(EntityTypeBuilder<CountryGroup> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasMany(x => x.Countries)
               .WithOne(c => c.CountryGroup)
               .HasForeignKey(c => c.CountryGroupId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
