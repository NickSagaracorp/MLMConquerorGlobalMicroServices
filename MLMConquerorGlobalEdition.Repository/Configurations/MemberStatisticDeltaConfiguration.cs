using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Sprint-16 — queue table for ancestor <c>MemberStatistics</c> deltas.
///
/// Two indexes:
///   1. (IsApplied, CreationDate) — the apply job's poll predicate. Hot path
///      scans only unapplied rows ordered by arrival, so the index lets it
///      claim a batch with a seek + range scan instead of a full table sweep.
///   2. (MemberId, IsApplied) — supports any per-member catch-up
///      diagnostic ("how far behind is upline X?") without thrashing the
///      apply-job index.
/// </summary>
public class MemberStatisticDeltaConfiguration : IEntityTypeConfiguration<MemberStatisticDelta>
{
    public void Configure(EntityTypeBuilder<MemberStatisticDelta> builder)
    {
        builder.ToTable("MemberStatisticDeltas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceMemberId).IsRequired().HasMaxLength(100);

        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => new { x.IsApplied, x.CreationDate });
        builder.HasIndex(x => new { x.MemberId, x.IsApplied });
    }
}
