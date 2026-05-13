using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Events;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CorporateContestConfiguration : IEntityTypeConfiguration<CorporateContest>
{
    public void Configure(EntityTypeBuilder<CorporateContest> builder)
    {
        builder.ToTable("CorporateContests");
        builder.HasKey(x => x.Id);

        // Align with the FK columns on translations + earnings (50 chars,
        // ample for a Guid string). Without the explicit length the base
        // string Id defaults to nvarchar(max) and breaks the FK creation.
        builder.Property(x => x.Id).HasMaxLength(50);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.BannerUrl).HasMaxLength(500);
        builder.Property(x => x.RulesUrl).HasMaxLength(500);

        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => new { x.StartDate, x.EndDate });

        builder.HasMany(x => x.Translations)
            .WithOne(t => t.Contest!)
            .HasForeignKey(t => t.ContestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CorporateContestTranslationConfiguration : IEntityTypeConfiguration<CorporateContestTranslation>
{
    public void Configure(EntityTypeBuilder<CorporateContestTranslation> builder)
    {
        builder.ToTable("CorporateContestTranslations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContestId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.BannerUrl).HasMaxLength(500);

        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.ContestId, x.LanguageCode }).IsUnique();
    }
}

public class CorporateContestEarningConfiguration : IEntityTypeConfiguration<CorporateContestEarning>
{
    public void Configure(EntityTypeBuilder<CorporateContestEarning> builder)
    {
        builder.ToTable("CorporateContestEarnings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContestId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.BeneficiaryMemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SourceMemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.SourceOrderId).IsRequired().HasMaxLength(50);

        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // Idempotency contract: the sweep job inserts at most one row per
        // (contest, order, upline). Re-running the sweep is a no-op.
        builder.HasIndex(x => new { x.ContestId, x.SourceOrderId, x.BeneficiaryMemberId }).IsUnique();
        builder.HasIndex(x => new { x.ContestId, x.BeneficiaryMemberId });

        builder.HasOne(x => x.Contest)
            .WithMany()
            .HasForeignKey(x => x.ContestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
