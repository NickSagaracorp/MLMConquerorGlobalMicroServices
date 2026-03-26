using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualTeamConfiguration : IEntityTypeConfiguration<DualTeamEntity>
{
    public void Configure(EntityTypeBuilder<DualTeamEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.LeftLegPoints).HasPrecision(18, 4);
        builder.Property(x => x.RightLegPoints).HasPrecision(18, 4);
        builder.HasIndex(x => x.MemberId);
        builder.HasIndex(x => x.HierarchyPath);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
