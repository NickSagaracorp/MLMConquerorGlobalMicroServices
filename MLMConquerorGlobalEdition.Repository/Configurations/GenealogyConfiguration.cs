using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class GenealogyConfiguration : IEntityTypeConfiguration<GenealogyEntity>
{
    public void Configure(EntityTypeBuilder<GenealogyEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // Unbounded materialized path — see DualTeamConfiguration for why it is not a
        // keyed index (1700-byte nonclustered-index limit vs. deep spillover chains).
        builder.Property(x => x.HierarchyPath).IsRequired();
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        builder.HasIndex(x => x.ParentMemberId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
