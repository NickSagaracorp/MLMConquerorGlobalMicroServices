using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class PayoutAttemptEarningConfiguration : IEntityTypeConfiguration<PayoutAttemptEarning>
{
    public void Configure(EntityTypeBuilder<PayoutAttemptEarning> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CommissionEarningId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.PayoutAttemptId);
        builder.HasIndex(x => x.CommissionEarningId);
    }
}
