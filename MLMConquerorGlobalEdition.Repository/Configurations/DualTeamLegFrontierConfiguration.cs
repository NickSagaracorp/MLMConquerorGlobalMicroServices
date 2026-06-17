using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualTeamLegFrontierConfiguration : IEntityTypeConfiguration<DualTeamLegFrontier>
{
    public void Configure(EntityTypeBuilder<DualTeamLegFrontier> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SponsorMemberId).IsRequired();
        // One frontier row per sponsor — the lock target for per-leg placement serialization.
        builder.HasIndex(x => x.SponsorMemberId).IsUnique();
    }
}
