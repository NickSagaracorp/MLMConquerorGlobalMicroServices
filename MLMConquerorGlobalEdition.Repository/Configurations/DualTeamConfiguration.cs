using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Services;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualTeamConfiguration : IEntityTypeConfiguration<DualTeamEntity>
{
    public void Configure(EntityTypeBuilder<DualTeamEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // Plan Q (Bloque 3): el binario no tiene ruta de texto (HierarchyPath, eliminada por RetiraRutaDelBinario).
        // La posicion es la clave compacta (BinaryPath, Depth) y el equipo, su rango (DualSubtree).
        builder.Property(x => x.LeftLegPoints).HasPrecision(18, 4);
        builder.Property(x => x.RightLegPoints).HasPrecision(18, 4);
        // A member may occupy at most ONE position in the dual tree. This unique
        // (filtered) index is the authoritative safety net against duplicate
        // placements: every placement writer (signup, manual, admin, AutoPlacementJob,
        // dev force-place) does a non-atomic "already placed?" check then inserts, so
        // concurrent writers could otherwise both insert the same MemberId. With this
        // constraint the racing insert fails instead of corrupting the tree.
        // Filtered on IsDeleted=0 so a soft-deleted node doesn't block re-placement.
        builder.HasIndex(x => x.MemberId).IsUnique().HasFilter("[IsDeleted] = 0");
        // Plain (non-filtered) lookup index on MemberId. The unique index above is FILTERED
        // (WHERE IsDeleted=0), which SQL Server cannot use to seek joins that don't also
        // filter IsDeleted — e.g. the recursive leg-point walk-up's `JOIN ... ON
        // pn.MemberId = w.ParentMemberId`. Without this, those joins fall back to clustered
        // scans (O(N) per hop). This index keeps every MemberId lookup a seek.
        builder.HasIndex(x => x.MemberId, "IX_DualTeamTree_MemberId_Lookup");
        // Plan M (M10): la CTE de DualSubtree y LegPointMaintenance buscan por ParentMemberId y leen
        // MemberId, Side e IsDeleted; incluidas, cada paso es un seek sin ir a la tabla.
        builder.HasIndex(x => x.ParentMemberId).IncludeProperties(x => new { x.MemberId, x.Side, x.IsDeleted });
        // Plan P: un hijo vivo por hueco. Es la red de DualTeamGate (que serializa por hueco) y lo que hace
        // segura la colocacion con o sin RCSI. Filtrado: un nodo borrado no ocupa el hueco y las raices
        // (ParentMemberId NULL) no cuentan. La clave se declara de 1.804 bytes (nvarchar(900) + int) y SQL
        // Server avisa del limite de 1.700; los ids reales tienen menos de 20 caracteres (Plan P, riesgo P-R4).
        builder.HasIndex(x => new { x.ParentMemberId, x.Side })
            .IsUnique()
            .HasDatabaseName("UX_DualTeamTree_ParentMemberId_Side")
            .HasFilter("[IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL");
        // Plan P (Bloque B): clave compacta. El subarbol de un nodo es el rango (Lo, Hi) de este indice
        // (DualBinaryPath). 8.223 niveles ocupan 1.032 bytes; el maximo (13.567) llena los 1.700.
        builder.Property(x => x.BinaryPath).HasMaxLength(DualBinaryPath.MaxBytes);   // varbinary(1700)
        builder.HasIndex(x => x.BinaryPath)
            .IsUnique()
            .HasDatabaseName("UX_DualTeamTree_BinaryPath")
            .HasFilter("[IsDeleted] = 0 AND [BinaryPath] IS NOT NULL")
            .IncludeProperties(x => new { x.MemberId, x.Depth, x.ParentMemberId, x.Side });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
