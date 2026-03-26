using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class GenealogyConfiguration : IEntityTypeConfiguration<GenealogyEntity>
{
    public void Configure(EntityTypeBuilder<GenealogyEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(2000);
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        builder.HasIndex(x => x.HierarchyPath);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
