using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CommissionEarningConfiguration : IEntityTypeConfiguration<CommissionEarning>
{
    public void Configure(EntityTypeBuilder<CommissionEarning> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.HasIndex(x => new { x.BeneficiaryMemberId, x.Status });
        builder.HasIndex(x => x.PeriodDate);
        builder.HasIndex(x => new { x.SourceOrderId, x.CommissionTypeId }).IsUnique()
            .HasFilter("[SourceOrderId] IS NOT NULL");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
