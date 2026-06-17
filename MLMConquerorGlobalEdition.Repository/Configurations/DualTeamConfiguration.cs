using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualTeamConfiguration : IEntityTypeConfiguration<DualTeamEntity>
{
    public void Configure(EntityTypeBuilder<DualTeamEntity> builder)
    {
        builder.HasKey(x => x.Id);
        // HierarchyPath is an unbounded materialized path. It is intentionally NOT a
        // B-tree index key: the deepest-on-side spillover rule produces deep chains
        // whose path length exceeds SQL Server's 1700-byte nonclustered-index key
        // limit, which previously made placement inserts fail. Subtree membership is
        // handled by the adjacency list (ParentMemberId) + incremental leg points,
        // not by indexed LIKE scans on this column.
        builder.Property(x => x.HierarchyPath).IsRequired();
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
        builder.HasIndex(x => x.ParentMemberId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
