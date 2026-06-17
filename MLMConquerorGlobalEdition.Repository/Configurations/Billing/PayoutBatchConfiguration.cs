using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class PayoutBatchConfiguration : IEntityTypeConfiguration<PayoutBatch>
{
    public void Configure(EntityTypeBuilder<PayoutBatch> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ExportCsvUrl).HasMaxLength(1000);
        builder.Property(x => x.ResultCsvUrl).HasMaxLength(1000);
        builder.Property(x => x.TotalAmountUsd).HasPrecision(18, 2);
        builder.Property(x => x.ReconciledBy).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => new { x.WalletType, x.ProcessDateUtc });
    }
}
