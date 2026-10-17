using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class GenealogyConfiguration : IEntityTypeConfiguration<GenealogyEntity>
{
    /// <summary>Índice único de Node; GenealogyNodes.IsDuplicateNode lo reconoce por el nombre.</summary>
    public const string NodeIndexName = "IX_GenealogyTree_Node";

    public void Configure(EntityTypeBuilder<GenealogyEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // Ruta de texto: se sigue escribiendo y la usan quienes parten la línea ascendente (Plan M, M15).
        // Ya no se filtra por ella: el equipo va por Node.
        builder.Property(x => x.HierarchyPath).IsRequired();
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        builder.HasIndex(x => x.ParentMemberId);

        // Plan M (M11): en profundidad, el subárbol es un rango contiguo del índice.
        builder.Property(x => x.Node);
        builder.HasIndex(x => x.Node)
            .IsUnique()
            .HasFilter("[Node] IS NOT NULL")
            .HasDatabaseName(NodeIndexName)
            .IncludeProperties(x => new { x.MemberId, x.Level });

        // Plan M (M12): en anchura, solo para reservar el ordinal de una raíz nueva.
        builder.Property<short?>("NodeLevel").HasComputedColumnSql("[Node].GetLevel()", stored: true);
        builder.HasIndex("NodeLevel", nameof(GenealogyEntity.Node));

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
