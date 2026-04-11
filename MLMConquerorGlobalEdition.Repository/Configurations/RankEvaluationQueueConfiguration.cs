using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RankEvaluationQueueConfiguration : IEntityTypeConfiguration<RankEvaluationQueue>
{
    public void Configure(EntityTypeBuilder<RankEvaluationQueue> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TriggerMemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.EvaluateMemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ProcessedBy).HasMaxLength(200);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Primary access pattern: job queries pending entries ordered by trigger date
        builder.HasIndex(x => new { x.IsProcessed, x.TriggerDate });

        // Dedup by evaluate target
        builder.HasIndex(x => x.EvaluateMemberId);

        // For diagnostics / audit
        builder.HasIndex(x => x.TriggerMemberId);
    }
}
