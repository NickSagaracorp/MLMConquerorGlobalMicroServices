using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class GenealogyConfiguration : IEntityTypeConfiguration<GenealogyEntity>
{
    /// <summary>Índice único de Node; GenealogyNodes.IsDuplicateNode lo reconoce por el nombre.</summary>
    public const string NodeIndexName = "IX_GenealogyTree_Node";

    /// <summary>Columna calculada que sustituye al filtro «[Node] IS NOT NULL» del índice único (10619).</summary>
    public const string NodeNullKey = "NodeNullKey";

    public void Configure(EntityTypeBuilder<GenealogyEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // Ruta de texto: se sigue escribiendo y la usan quienes parten la línea ascendente (Plan M, M15).
        // Ya no se filtra por ella: el equipo va por Node.
        builder.Property(x => x.HierarchyPath).IsRequired();
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        builder.HasIndex(x => x.ParentMemberId);

        // Plan M (M11): en profundidad, el subárbol es un rango contiguo del índice (Node va primero).
        // Único entre los Node no nulos. SQL Server no admite un filtro sobre una columna CLR (10619) ni
        // sobre una calculada, así que la segunda clave, NodeNullKey, hace el papel del filtro: vale el Id
        // si Node es nulo (las filas aún sin nodo no chocan entre sí) y NULL si no lo es (dos filas con el
        // mismo Node chocan como (Node, NULL)). La clave mide 892 + 100 bytes como mucho, por debajo de 1700.
        builder.Property(x => x.Node);
        builder.Property<string?>(NodeNullKey)
            .HasColumnType("varchar(100)")
            .HasComputedColumnSql("CASE WHEN [Node] IS NULL THEN CAST([Id] AS varchar(100)) END", stored: true);
        builder.HasIndex(nameof(GenealogyEntity.Node), NodeNullKey)
            .IsUnique()
            // Sin el filtro que EF pone por defecto a un índice único con columnas que admiten nulos (10619).
            .HasFilter(null)
            .HasDatabaseName(NodeIndexName)
            // Cubre CurrentUnder entero: el nodo vigente (CreationDate) y el filtro global (IsDeleted). Sin
            // ellas, unido a los perfiles, el optimizador recorre la tabla en vez de hacer el seek.
            .IncludeProperties(x => new { x.MemberId, x.Level, x.CreationDate, x.IsDeleted });

        // Plan M (M12): en anchura, solo para reservar el ordinal de una raíz nueva.
        builder.Property<short?>("NodeLevel").HasComputedColumnSql("[Node].GetLevel()", stored: true);
        builder.HasIndex("NodeLevel", nameof(GenealogyEntity.Node));

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
