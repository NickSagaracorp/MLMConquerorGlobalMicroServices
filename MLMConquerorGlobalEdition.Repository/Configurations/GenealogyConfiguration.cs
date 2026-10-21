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

    /// <summary>CHECK de ordinales enteros (revisión M4).</summary>
    public const string NodeIntegerOrdinalsCheck = "CK_GenealogyTree_Node_OrdinalesEnteros";

    /// <summary>Plan Q (DQ9): un solo nodo vivo por miembro.</summary>
    public const string LiveMemberIndexName = "UX_GenealogyTree_MemberId_Vivo";

    /// <summary>Plan Q (revision del bloque 5, I1): estrecho, para contar los nodos vivos de un equipo.</summary>
    public const string LiveNodeIndexName = "IX_GenealogyTree_Node_Vivo";

    public void Configure(EntityTypeBuilder<GenealogyEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // Ruta de texto: se sigue escribiendo y la usan quienes parten la línea ascendente (Plan M, M15).
        // Ya no se filtra por ella: el equipo va por Node.
        builder.Property(x => x.HierarchyPath).IsRequired();
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        // Plan Q (DQ9): un solo nodo vivo por miembro. Con el, el nodo vigente es el unico y CurrentUnder no necesita la
        // antijoin (medida en el Plan P: el conteo del equipo de inscripcion tardaba 2,8 s con 1.000.000).
        builder.HasIndex(new[] { nameof(GenealogyEntity.MemberId) }, LiveMemberIndexName)
            .IsUnique().HasFilter("[IsDeleted] = 0");
        // Revisión M5: los hermanos de un nodo (NextChildAsync, el relleno) se buscan por ParentMemberId y se
        // leen por Node; incluido, sin ir a la tabla.
        builder.HasIndex(x => x.ParentMemberId).IncludeProperties(x => x.Node);

        // Plan M (M11): en profundidad, el subárbol es un rango contiguo del índice (Node va primero).
        // Único entre los Node no nulos. SQL Server no admite un filtro sobre una columna CLR (10619) ni
        // sobre una calculada, así que la segunda clave, NodeNullKey, hace el papel del filtro: vale el Id
        // si Node es nulo (las filas aún sin nodo no chocan entre sí) y NULL si no lo es (dos filas con el
        // mismo Node chocan como (Node, NULL)). La clave mide 892 + 200 bytes como mucho, por debajo de 1700.
        builder.Property(x => x.Node);
        builder.Property<string?>(NodeNullKey)
            .HasColumnType("nvarchar(100)")
            .HasComputedColumnSql("CASE WHEN [Node] IS NULL THEN CAST([Id] AS nvarchar(100)) END", stored: true);
        builder.HasIndex(nameof(GenealogyEntity.Node), NodeNullKey)
            .IsUnique()
            // Sin el filtro que EF pone por defecto a un índice único con columnas que admiten nulos (10619).
            .HasFilter(null)
            .HasDatabaseName(NodeIndexName)
            // Cubre CurrentUnder entero: el nodo vigente (CreationDate) y el filtro global (IsDeleted). Sin
            // ellas, unido a los perfiles, el optimizador recorre la tabla en vez de hacer el seek.
            .IncludeProperties(x => new { x.MemberId, x.Level, x.CreationDate, x.IsDeleted });

        // Plan Q, revision del bloque 5 (I1): el total de Mi equipo y de los eventos cuenta los nodos vivos del rango. Este
        // indice solo lleva Node (y el Id de la fila): 1.370 paginas frente a 1.910 del anterior con 119.000 en local.
        builder.HasIndex(new[] { nameof(GenealogyEntity.Node) }, LiveNodeIndexName).HasFilter("[IsDeleted] = 0");

        // Plan M (M12): en anchura, solo para reservar el ordinal de una raíz nueva.
        builder.Property<short?>("NodeLevel").HasComputedColumnSql("[Node].GetLevel()", stored: true);
        builder.HasIndex("NodeLevel", nameof(GenealogyEntity.Node));

        // Revisión M4: GenealogySubtree.Under lee el equipo como el rango [nodo, hermano siguiente), exacto
        // solo si ningún ordinal es fraccionario («/1.1/», lo que daría GetDescendant entre dos hermanos).
        builder.ToTable(t => t.HasCheckConstraint(NodeIntegerOrdinalsCheck,
            "[Node] IS NULL OR CHARINDEX('.', [Node].ToString()) = 0"));

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
