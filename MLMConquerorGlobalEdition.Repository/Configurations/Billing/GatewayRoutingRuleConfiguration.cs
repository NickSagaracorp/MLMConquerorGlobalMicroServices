using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class GatewayRoutingRuleConfiguration : IEntityTypeConfiguration<GatewayRoutingRule>
{
    public void Configure(EntityTypeBuilder<GatewayRoutingRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsoCountryCode).HasMaxLength(3);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Spec: index on (OperationType, CardBrand)
        builder.HasIndex(x => new { x.OperationType, x.CardBrand });
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.CurrencyPolicy)
               .WithMany()
               .HasForeignKey(x => x.CurrencyPolicyId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CountryGroup)
               .WithMany()
               .HasForeignKey(x => x.CountryGroupId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Splits)
               .WithOne(s => s.GatewayRoutingRule)
               .HasForeignKey(s => s.GatewayRoutingRuleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
