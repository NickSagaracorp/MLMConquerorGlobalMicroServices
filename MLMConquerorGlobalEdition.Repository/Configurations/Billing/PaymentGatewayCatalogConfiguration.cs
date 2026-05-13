using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class PaymentGatewayCatalogConfiguration : IEntityTypeConfiguration<PaymentGatewayCatalog>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayCatalog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.Processor).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}
