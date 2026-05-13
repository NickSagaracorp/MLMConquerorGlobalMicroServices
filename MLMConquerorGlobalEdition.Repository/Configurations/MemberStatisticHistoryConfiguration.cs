using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Monthly snapshot table for <see cref="MemberStatisticEntity"/>. Unique
/// (MemberId, SnapshotYear, SnapshotMonth) so the nightly job can upsert the
/// current month's row idempotently without growing without bound; the
/// composite index also serves the chart's range scan when the UI asks for
/// "last 6 months for member X".
/// </summary>
public class MemberStatisticHistoryConfiguration : IEntityTypeConfiguration<MemberStatisticHistoryEntity>
{
    public void Configure(EntityTypeBuilder<MemberStatisticHistoryEntity> builder)
    {
        builder.ToTable("MemberStatisticHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);

        builder.HasIndex(x => new { x.MemberId, x.SnapshotYear, x.SnapshotMonth }).IsUnique();
        builder.HasIndex(x => new { x.SnapshotYear, x.SnapshotMonth });

        builder.Property(x => x.LeftLegPoints).HasPrecision(18, 2);
        builder.Property(x => x.RightLegPoints).HasPrecision(18, 2);
        builder.Property(x => x.CurrentWeekIncomeGrowth).HasPrecision(18, 2);
        builder.Property(x => x.CurrentMonthIncomeGrowth).HasPrecision(18, 2);
        builder.Property(x => x.CurrentYearIncomeGrowth).HasPrecision(18, 2);

        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
    }
}
