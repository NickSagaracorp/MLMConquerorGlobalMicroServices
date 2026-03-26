using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class GhostPointConfiguration : IEntityTypeConfiguration<GhostPointEntity>
{
    public void Configure(EntityTypeBuilder<GhostPointEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Points).HasPrecision(18, 4);
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.LegMemberId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
