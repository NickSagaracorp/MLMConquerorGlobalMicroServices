using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RegionGatewayConfiguration : IEntityTypeConfiguration<RegionGateway>
{
    public void Configure(EntityTypeBuilder<RegionGateway> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GatewayType).IsRequired();
        builder.Property(x => x.Priority).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // One region cannot have the same gateway twice
        builder.HasIndex(x => new { x.RegionId, x.GatewayType }).IsUnique();

        builder.HasIndex(x => x.RegionId);
        builder.HasIndex(x => x.IsActive);
    }
}
