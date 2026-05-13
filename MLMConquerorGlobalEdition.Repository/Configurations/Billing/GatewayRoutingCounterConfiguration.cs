using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class GatewayRoutingCounterConfiguration : IEntityTypeConfiguration<GatewayRoutingCounter>
{
    public void Configure(EntityTypeBuilder<GatewayRoutingCounter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RouteBucketKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Spec: index on RouteBucketKey
        builder.HasIndex(x => x.RouteBucketKey);
        builder.HasIndex(x => new { x.RouteBucketKey, x.CardProcessor }).IsUnique();
    }
}
