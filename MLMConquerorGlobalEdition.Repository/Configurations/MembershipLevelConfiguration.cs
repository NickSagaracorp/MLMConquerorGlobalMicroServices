using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// MWR Life Travel Advantage membership tiers from the comp plan.
/// LevelNo on CommissionType (IsSponsorBonus=true) maps to these IDs to select the correct Member Bonus.
///   ID 1 = Free / Lifestyle Ambassador (builder annual fee — no Member Bonus)
///   ID 2 = VIP          → Member Bonus $20  (1 qualification point/month)
///   ID 3 = Elite         → Member Bonus $40  (6 qualification points/month)
///   ID 4 = Turbo         → Member Bonus $80  (6 qualification points/month)
/// Prices are placeholders — configure via AdminAPI before going live.
/// </summary>
public class MembershipLevelConfiguration : IEntityTypeConfiguration<MembershipLevel>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<MembershipLevel> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Price).HasColumnType("decimal(10,2)");
        builder.Property(x => x.RenewalPrice).HasColumnType("decimal(10,2)");

        builder.HasData(

            // ── Lifestyle Ambassador (builder) ────────────────────────────────
            // Annual business fee. No Member Bonus triggered for this level.
            new MembershipLevel
            {
                Id = 1,
                Name         = "Lifestyle Ambassador",
                Description  = "Annual business fee for team-building ambassadors. Qualifies for all commissions.",
                Price        = 99m, RenewalPrice = 99m,
                SortOrder    = 1,
                IsFree       = false, IsAutoRenew = true, IsActive = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ── Travel Advantage – VIP ────────────────────────────────────────
            // 1 qualification point per month (after first rebilling).
            // Triggers $20 Member Bonus to the direct enroller.
            new MembershipLevel
            {
                Id = 2,
                Name         = "Travel Advantage – VIP",
                Description  = "Entry-level travel membership. 1 qualification point/month. Triggers $20 Member Bonus to enroller.",
                Price        = 40m, RenewalPrice = 40m,
                SortOrder    = 2,
                IsFree       = false, IsAutoRenew = true, IsActive = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ── Travel Advantage – Elite ──────────────────────────────────────
            // 6 qualification points per month. Triggers $40 Member Bonus.
            new MembershipLevel
            {
                Id = 3,
                Name         = "Travel Advantage – Elite",
                Description  = "Full travel membership. 6 qualification points/month. Triggers $40 Member Bonus to enroller.",
                Price        = 99m, RenewalPrice = 99m,
                SortOrder    = 3,
                IsFree       = false, IsAutoRenew = true, IsActive = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ── Travel Advantage – Turbo ──────────────────────────────────────
            // 6 qualification points per month. Triggers $80 Member Bonus.
            new MembershipLevel
            {
                Id = 4,
                Name         = "Travel Advantage – Turbo",
                Description  = "Premium travel membership. 6 qualification points/month. Triggers $80 Member Bonus to enroller.",
                Price        = 199m, RenewalPrice = 199m,
                SortOrder    = 4,
                IsFree       = false, IsAutoRenew = true, IsActive = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            }
        );
    }
}
