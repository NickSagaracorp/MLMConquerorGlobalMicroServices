using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class CurrencyPolicyConfiguration : IEntityTypeConfiguration<CurrencyPolicy>
{
    public void Configure(EntityTypeBuilder<CurrencyPolicy> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PresentmentCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.MarkupPercent).HasPrecision(5, 2);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.PresentmentCurrency).IsUnique();
    }
}
