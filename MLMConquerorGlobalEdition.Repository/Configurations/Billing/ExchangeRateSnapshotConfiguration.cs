using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class ExchangeRateSnapshotConfiguration : IEntityTypeConfiguration<ExchangeRateSnapshot>
{
    public void Configure(EntityTypeBuilder<ExchangeRateSnapshot> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BaseCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.QuoteCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Rate).HasPrecision(18, 8);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Spec: index on QuoteCurrency + ExpiresAtUtc
        builder.HasIndex(x => new { x.QuoteCurrency, x.ExpiresAtUtc });
    }
}
