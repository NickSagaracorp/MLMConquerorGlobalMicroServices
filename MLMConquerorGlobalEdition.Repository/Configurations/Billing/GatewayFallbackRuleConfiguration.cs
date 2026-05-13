using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class GatewayFallbackRuleConfiguration : IEntityTypeConfiguration<GatewayFallbackRule>
{
    public void Configure(EntityTypeBuilder<GatewayFallbackRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.OperationType, x.PrimaryProcessor, x.StepOrder }).IsUnique();
    }
}
