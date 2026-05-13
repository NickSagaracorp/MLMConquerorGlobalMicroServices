using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class GatewayRoutingRuleSplitConfiguration : IEntityTypeConfiguration<GatewayRoutingRuleSplit>
{
    public void Configure(EntityTypeBuilder<GatewayRoutingRuleSplit> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WeightPercent).HasPrecision(5, 2);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);
    }
}
