using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Commission;

public class DailyResidualEarningConfiguration : IEntityTypeConfiguration<DailyResidualEarning>
{
    public void Configure(EntityTypeBuilder<DailyResidualEarning> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BeneficiaryMemberId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Amount).HasPrecision(18, 4);
        builder.Property(x => x.SourceOrderId).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.ConsolidatedIntoCommissionEarningId).HasMaxLength(100);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // ── Snapshot columns (nullable; no Data Annotations in Domain) ──────────
        // CurrentRankId: nullable int snapshot of RankDefinition.Id — no FK constraint
        // (snapshot reference, not a live relation; the rank row must not cascade-delete this row).
        builder.Property(x => x.CurrentRankId).IsRequired(false);
        builder.Property(x => x.EligibleDualTeamPoints).IsRequired(false);
        builder.Property(x => x.EligibleEnrollmentTeamPoints).IsRequired(false);
        builder.Property(x => x.PersonalPoints).IsRequired(false);

        // ── Payment-tracking columns (nullable; set when Status → Paid) ──────────
        builder.Property(x => x.PaymentDate).IsRequired(false);
        builder.Property(x => x.CommentedBy).IsRequired(false).HasMaxLength(200);
        builder.Property(x => x.PaymentComment).IsRequired(false).HasMaxLength(500);

        // Spec: indexes on BeneficiaryMemberId + Status, and EarnedDate
        builder.HasIndex(x => new { x.BeneficiaryMemberId, x.Status });
        builder.HasIndex(x => x.EarnedDate);
    }
}
