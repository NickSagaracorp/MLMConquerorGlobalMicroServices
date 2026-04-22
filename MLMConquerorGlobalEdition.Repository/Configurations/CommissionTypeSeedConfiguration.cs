using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Commission types derived from the MWR Life Lifestyle Ambassador Compensation Plan (EN-25.05.05).
///
/// ┌─────────────────────────────────────────────────────────────────────────────┐
/// │  ID MAP — never change IDs; ReverseId references depend on them            │
/// ├─────┬────────────────────────────────────────┬──────────────────────────── │
/// │  1  │ Member Bonus – VIP                     │ Cat 1 · IsSponsorBonus      │
/// │  2  │ Member Bonus – Elite                   │ Cat 1 · IsSponsorBonus      │
/// │  3  │ Member Bonus – Turbo                   │ Cat 1 · IsSponsorBonus      │
/// │  4  │ Fast Start Bonus – Window 1            │ Cat 2 · TriggerOrder=1      │
/// │  5  │ Fast Start Bonus – Window 2            │ Cat 2 · TriggerOrder=2      │
/// │  6  │ Fast Start Bonus – Window 3            │ Cat 2 · TriggerOrder=3      │
/// │  7  │ DTR – Silver                           │ Cat 3 · $4/day              │
/// │  8  │ DTR – Gold                             │ Cat 3 · $10/day             │
/// │  9  │ DTR – Platinum                         │ Cat 3 · $15/day             │
/// │ 10  │ DTR – Titanium                         │ Cat 3 · $25/day             │
/// │ 11  │ DTR – Jade                             │ Cat 3 · $40/day             │
/// │ 12  │ DTR – Pearl                            │ Cat 3 · $80/day             │
/// │ 13  │ DTR – Emerald                          │ Cat 3 · $150/day            │
/// │ 14  │ DTR – Ruby                             │ Cat 3 · $300/day            │
/// │ 15  │ DTR – Sapphire                         │ Cat 3 · $500/day            │
/// │ 16  │ DTR – Diamond                          │ Cat 3 · $750/day            │
/// │ 17  │ DTR – Double Diamond                   │ Cat 3 · $1,000/day          │
/// │ 18  │ DTR – Triple Diamond                   │ Cat 3 · $1,500/day          │
/// │ 19  │ DTR – Blue Diamond                     │ Cat 3 · $2,000/day          │
/// │ 20  │ DTR – Black Diamond                    │ Cat 3 · $3,000/day          │
/// │ 21  │ DTR – Royal                            │ Cat 3 · $4,000/day          │
/// │ 22  │ DTR – Double Royal                     │ Cat 3 · $5,000/day          │
/// │ 23  │ DTR – Triple Royal                     │ Cat 3 · $7,500/day          │
/// │ 24  │ DTR – Blue Royal                       │ Cat 3 · $10,000/day         │
/// │ 25  │ DTR – Black Royal                      │ Cat 3 · $15,000/day         │
/// │ 26  │ Boost Bonus – Gold                     │ Cat 4 · $600, 6 members     │
/// │ 27  │ Boost Bonus – Platinum                 │ Cat 4 · $1,200, 12 members  │
/// │ 28  │ Presidential Bonus                     │ Cat 4 · 20% 2nd leg monthly │
/// │ 29  │ Reversal – Member Bonus VIP            │ Cat 5 · reverses ID 1       │
/// │ 30  │ Reversal – Member Bonus Elite          │ Cat 5 · reverses ID 2       │
/// │ 31  │ Reversal – Member Bonus Turbo          │ Cat 5 · reverses ID 3       │
/// │ 32  │ Reversal – Fast Start Bonus            │ Cat 5 · reverses IDs 4–6    │
/// │ 33  │ Builder Bonus – VIP                    │ Cat 6 · IsSponsorBonus      │
/// │ 34  │ Builder Bonus – Elite                  │ Cat 6 · IsSponsorBonus      │
/// │ 35  │ Builder Bonus – Turbo                  │ Cat 6 · IsSponsorBonus      │
/// │ 36  │ Builder Bonus Turbo – VIP              │ Cat 7 · IsSponsorBonus      │
/// │ 37  │ Builder Bonus Turbo – Elite            │ Cat 7 · IsSponsorBonus      │
/// │ 38  │ Builder Bonus Turbo – Turbo            │ Cat 7 · IsSponsorBonus      │
/// │ 39  │ Admin Fee                              │ Cat 8 · % deduction/payout  │
/// │ 40  │ Token Deduction                        │ Cat 8 · per token consumed  │
/// │ 41  │ Reversal – Builder Bonus VIP           │ Cat 5 · reverses ID 33      │
/// │ 42  │ Reversal – Builder Bonus Elite         │ Cat 5 · reverses ID 34      │
/// │ 43  │ Reversal – Builder Bonus Turbo         │ Cat 5 · reverses ID 35      │
/// │ 44  │ Reversal – Builder Bonus Turbo VIP     │ Cat 5 · reverses ID 36      │
/// │ 45  │ Reversal – Builder Bonus Turbo Elite   │ Cat 5 · reverses ID 37      │
/// │ 46  │ Reversal – Builder Bonus Turbo Turbo   │ Cat 5 · reverses ID 38      │
/// │ 47  │ BB Elite – Silver (rank 1)             │ Cat 6 · LifeTimeRank=1      │
/// │ 48  │ BB Elite – Gold (rank 2)               │ Cat 6 · LifeTimeRank=2      │
/// │ 49  │ BB Elite – Platinum (rank 3)           │ Cat 6 · LifeTimeRank=3      │
/// │ 50  │ BB Elite – Titanium (rank 4)           │ Cat 6 · LifeTimeRank=4      │
/// │ 51  │ BB Elite – Jade (rank 5)               │ Cat 6 · LifeTimeRank=5      │
/// │ 52  │ BB Elite – Pearl (rank 6)              │ Cat 6 · LifeTimeRank=6      │
/// │ 53  │ BB Elite – Emerald (rank 7)            │ Cat 6 · LifeTimeRank=7      │
/// │ 54  │ BB Elite – Ruby (rank 8)               │ Cat 6 · LifeTimeRank=8      │
/// │ 55  │ BB Elite – Sapphire (rank 9)           │ Cat 6 · LifeTimeRank=9      │
/// │ 56  │ BB Elite – Diamond (rank 10)           │ Cat 6 · LifeTimeRank=10     │
/// │ 57  │ BB Elite – Double Diamond (rank 11)    │ Cat 6 · LifeTimeRank=11     │
/// │ 58  │ BB Elite – Triple Diamond (rank 12)    │ Cat 6 · LifeTimeRank=12     │
/// │ 59  │ BB Elite – Blue Diamond (rank 13)      │ Cat 6 · LifeTimeRank=13     │
/// │ 60  │ BB Elite – Black Diamond (rank 14)     │ Cat 6 · LifeTimeRank=14     │
/// │ 61  │ BB Elite – Royal (rank 15)             │ Cat 6 · LifeTimeRank=15     │
/// │ 62  │ BB Elite – Double Royal (rank 16)      │ Cat 6 · LifeTimeRank=16     │
/// │ 63  │ BB Elite – Triple Royal (rank 17)      │ Cat 6 · LifeTimeRank=17     │
/// │ 64  │ BB Elite – Blue Royal (rank 18)        │ Cat 6 · LifeTimeRank=18     │
/// │ 65  │ BB Elite – Black Royal (rank 19)       │ Cat 6 · LifeTimeRank=19     │
/// │ 66  │ BB Turbo – Silver (rank 1)             │ Cat 7 · LifeTimeRank=1      │
/// │ 67  │ BB Turbo – Gold (rank 2)               │ Cat 7 · LifeTimeRank=2      │
/// │ 68  │ BB Turbo – Platinum (rank 3)           │ Cat 7 · LifeTimeRank=3      │
/// │ 69  │ BB Turbo – Titanium (rank 4)           │ Cat 7 · LifeTimeRank=4      │
/// │ 70  │ BB Turbo – Jade (rank 5)               │ Cat 7 · LifeTimeRank=5      │
/// │ 71  │ BB Turbo – Pearl (rank 6)              │ Cat 7 · LifeTimeRank=6      │
/// │ 72  │ BB Turbo – Emerald (rank 7)            │ Cat 7 · LifeTimeRank=7      │
/// │ 73  │ BB Turbo – Ruby (rank 8)               │ Cat 7 · LifeTimeRank=8      │
/// │ 74  │ BB Turbo – Sapphire (rank 9)           │ Cat 7 · LifeTimeRank=9      │
/// │ 75  │ BB Turbo – Diamond (rank 10)           │ Cat 7 · LifeTimeRank=10     │
/// │ 76  │ BB Turbo – Double Diamond (rank 11)    │ Cat 7 · LifeTimeRank=11     │
/// │ 77  │ BB Turbo – Triple Diamond (rank 12)    │ Cat 7 · LifeTimeRank=12     │
/// │ 78  │ BB Turbo – Blue Diamond (rank 13)      │ Cat 7 · LifeTimeRank=13     │
/// │ 79  │ BB Turbo – Black Diamond (rank 14)     │ Cat 7 · LifeTimeRank=14     │
/// │ 80  │ BB Turbo – Royal (rank 15)             │ Cat 7 · LifeTimeRank=15     │
/// │ 81  │ BB Turbo – Double Royal (rank 16)      │ Cat 7 · LifeTimeRank=16     │
/// │ 82  │ BB Turbo – Triple Royal (rank 17)      │ Cat 7 · LifeTimeRank=17     │
/// │ 83  │ BB Turbo – Blue Royal (rank 18)        │ Cat 7 · LifeTimeRank=18     │
/// │ 84  │ BB Turbo – Black Royal (rank 19)       │ Cat 7 · LifeTimeRank=19     │
/// │ 85  │ Car Bonus                              │ Cat 4 · $500/mo, 1000 ET pts│
/// └─────┴────────────────────────────────────────┴──────────────────────────── │
///
/// TeamPoints for DTR Silver/Gold/Platinum = Enrollment Team points (ET-based ranks).
/// TeamPoints for Titanium+ = Dual Team points. Handler should be updated to check
/// MemberStatisticEntity.EnrollmentPoints for IDs 7-9 (future iteration).
///
/// Point values per member type (comp plan):
///   Elite / Turbo  = 6 pts
///   VIP / VIP 180  = 1 pt  (after first rebilling)
///
/// Builder Bonus (Cat 6) and Builder Bonus Turbo (Cat 7) are additional sponsor
/// bonuses layered on top of Member Bonus (Cat 1). Amounts are defaults — adjust
/// via admin panel to match the active comp plan version. Both sets have their own
/// reversal types (IDs 41-46) following the same 14-day chargeback rule.
///
/// Admin Fee (ID 39): percentage deducted from total commission payout at batch
/// processing. Percentage field holds the rate (default 5%). Engine must check
/// CommissionCategoryId == 8 to handle as a deduction.
///
/// Token Deduction (ID 40): applied in real-time when tokens are consumed.
/// Amount is determined per-transaction by the token engine; Amount here
/// is the default unit cost that can be overridden per TokenType.
/// </summary>
public class CommissionTypeSeedConfiguration : IEntityTypeConfiguration<CommissionType>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<CommissionType> builder)
    {
        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");

        builder.HasData(

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 1 — ENROLLMENT BONUSES (Member Bonus)
            // Fixed dollar amount per membership tier enrolled. Paid 4 days later.
            // IsSponsorBonus = true → SponsorBonusService selects by LevelNo matching membership level ID.
            // LevelNo 2=VIP, 3=Elite, 4=Turbo (matches MembershipLevel IDs).
            // ReverseId points to the matching reversal type (IDs 29-31).
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 1, CommissionCategoryId = 1,
                Name        = "Member Bonus – VIP",
                Description = "One-time $20 bonus to the direct enroller when a VIP member signs up.",
                Percentage  = 0m, Amount = 20m,
                PaymentDelayDays = 4,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 2,   // matches MembershipLevel.Id = 2 (VIP)
                ReverseId = 29,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 2, CommissionCategoryId = 1,
                Name        = "Member Bonus – Elite",
                Description = "One-time $40 bonus to the direct enroller when an Elite member signs up.",
                Percentage  = 0m, Amount = 40m,
                PaymentDelayDays = 4,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 3,   // matches MembershipLevel.Id = 3 (Elite)
                ReverseId = 30,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 3, CommissionCategoryId = 1,
                Name        = "Member Bonus – Turbo",
                Description = "One-time $40 bonus paid to the direct enroller for the Turbo portion. Stacks with Member Bonus – Elite ($40) for a combined $80 on Turbo signups.",
                Percentage  = 0m, Amount = 40m,
                PaymentDelayDays = 4,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 4,   // matches MembershipLevel.Id = 4 (Turbo)
                ReverseId = 31,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 2 — FAST START BONUS (3 windows, $150 each)
            // TriggerOrder = window number (1/2/3).
            // Window 1: 14 days from enrollment.
            // Window 2: 7 days from reset 1 (after earning W1 bonus).
            // Window 3: 7 days from reset 2 (after earning W2 bonus).
            // Paid per qualifying enrollment within the active window.
            // All three windows pay the same fixed amount ($150).
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 4, CommissionCategoryId = 2,
                Name        = "Fast Start Bonus – Window 1",
                Description = "Earn $150 when enrolling within your first 14 days as an ambassador.",
                Percentage  = 0m, Amount = 150m,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = false,
                LevelNo = 1, TriggerOrder = 1,
                DaysAfterJoining = 14,
                ReverseId = 32,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 5, CommissionCategoryId = 2,
                Name        = "Fast Start Bonus – Window 2",
                Description = "Earn $150 within 7 days of triggering Reset 1 (after earning Window 1 bonus).",
                Percentage  = 0m, Amount = 150m,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = false,
                LevelNo = 1, TriggerOrder = 2,
                DaysAfterJoining = 7,
                ReverseId = 32,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 6, CommissionCategoryId = 2,
                Name        = "Fast Start Bonus – Window 3",
                Description = "Earn $150 within 7 days of triggering Reset 2 (after earning Window 2 bonus).",
                Percentage  = 0m, Amount = 150m,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = false,
                LevelNo = 1, TriggerOrder = 3,
                DaysAfterJoining = 7,
                ReverseId = 32,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 3 — DUAL TEAM RESIDUALS (DTR)
            // Fixed daily earnings per rank. Handler selects the HIGHEST qualifying tier.
            // TeamPoints = minimum qualifying points threshold.
            // Silver/Gold/Platinum: ET-based (EnrollmentPoints in MemberStatisticEntity).
            // Titanium+: DT-based (DualTeamPoints). Handler update needed for ET distinction.
            //   ET: Silver=18 (3×6), Gold=72 (12×6), Platinum=175
            //   DT: per rank requirement table in comp plan.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType { Id = 7,  CommissionCategoryId = 3, Name = "DTR – Silver",        Description = "Earn $4/day when qualifying at Silver rank (18 Enrollment Team points).",       Percentage = 0m, Amount = 4m,     PaymentDelayDays = 4, TeamPoints = 18,     IsActive = true, ResidualBased = true, IsEnrollmentBased = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 8,  CommissionCategoryId = 3, Name = "DTR – Gold",          Description = "Earn $10/day when qualifying at Gold rank (72 Enrollment Team points).",       Percentage = 0m, Amount = 10m,    PaymentDelayDays = 4, TeamPoints = 72,     IsActive = true, ResidualBased = true, IsEnrollmentBased = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 9,  CommissionCategoryId = 3, Name = "DTR – Platinum",      Description = "Earn $15/day when qualifying at Platinum rank (175 Enrollment Team points).",  Percentage = 0m, Amount = 15m,    PaymentDelayDays = 4, TeamPoints = 175,    IsActive = true, ResidualBased = true, IsEnrollmentBased = true,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 10, CommissionCategoryId = 3, Name = "DTR – Titanium",      Description = "Earn $25/day when qualifying at Titanium rank (350 Dual Team points).",        Percentage = 0m, Amount = 25m,    PaymentDelayDays = 4, TeamPoints = 350,    IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 11, CommissionCategoryId = 3, Name = "DTR – Jade",          Description = "Earn $40/day when qualifying at Jade rank (700 Dual Team points).",            Percentage = 0m, Amount = 40m,    PaymentDelayDays = 4, TeamPoints = 700,    IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 12, CommissionCategoryId = 3, Name = "DTR – Pearl",         Description = "Earn $80/day when qualifying at Pearl rank (1,500 Dual Team points).",         Percentage = 0m, Amount = 80m,    PaymentDelayDays = 4, TeamPoints = 1500,   IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 13, CommissionCategoryId = 3, Name = "DTR – Emerald",       Description = "Earn $150/day when qualifying at Emerald rank (3,000 Dual Team points).",      Percentage = 0m, Amount = 150m,   PaymentDelayDays = 4, TeamPoints = 3000,   IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 14, CommissionCategoryId = 3, Name = "DTR – Ruby",          Description = "Earn $300/day when qualifying at Ruby rank (6,000 Dual Team points).",         Percentage = 0m, Amount = 300m,   PaymentDelayDays = 4, TeamPoints = 6000,   IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 15, CommissionCategoryId = 3, Name = "DTR – Sapphire",      Description = "Earn $500/day when qualifying at Sapphire rank (10,000 Dual Team points).",    Percentage = 0m, Amount = 500m,   PaymentDelayDays = 4, TeamPoints = 10000,  IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 16, CommissionCategoryId = 3, Name = "DTR – Diamond",       Description = "Earn $750/day when qualifying at Diamond rank (15,000 Dual Team points).",     Percentage = 0m, Amount = 750m,   PaymentDelayDays = 4, TeamPoints = 15000,  IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 17, CommissionCategoryId = 3, Name = "DTR – Double Diamond",Description = "Earn $1,000/day when qualifying at Double Diamond (20,000 DT points).",        Percentage = 0m, Amount = 1000m,  PaymentDelayDays = 4, TeamPoints = 20000,  IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 18, CommissionCategoryId = 3, Name = "DTR – Triple Diamond",Description = "Earn $1,500/day when qualifying at Triple Diamond (30,000 DT points).",        Percentage = 0m, Amount = 1500m,  PaymentDelayDays = 4, TeamPoints = 30000,  IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 19, CommissionCategoryId = 3, Name = "DTR – Blue Diamond",  Description = "Earn $2,000/day when qualifying at Blue Diamond (60,000 DT points).",          Percentage = 0m, Amount = 2000m,  PaymentDelayDays = 4, TeamPoints = 60000,  IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 20, CommissionCategoryId = 3, Name = "DTR – Black Diamond", Description = "Earn $3,000/day when qualifying at Black Diamond (120,000 DT points).",        Percentage = 0m, Amount = 3000m,  PaymentDelayDays = 4, TeamPoints = 120000, IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 21, CommissionCategoryId = 3, Name = "DTR – Royal",         Description = "Earn $4,000/day when qualifying at Royal rank (200,000 DT points).",           Percentage = 0m, Amount = 4000m,  PaymentDelayDays = 4, TeamPoints = 200000, IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 22, CommissionCategoryId = 3, Name = "DTR – Double Royal",  Description = "Earn $5,000/day when qualifying at Double Royal (300,000 DT points).",         Percentage = 0m, Amount = 5000m,  PaymentDelayDays = 4, TeamPoints = 300000, IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 23, CommissionCategoryId = 3, Name = "DTR – Triple Royal",  Description = "Earn $7,500/day when qualifying at Triple Royal (400,000 DT points).",         Percentage = 0m, Amount = 7500m,  PaymentDelayDays = 4, TeamPoints = 400000, IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 24, CommissionCategoryId = 3, Name = "DTR – Blue Royal",    Description = "Earn $10,000/day when qualifying at Blue Royal (500,000 DT points).",          Percentage = 0m, Amount = 10000m, PaymentDelayDays = 4, TeamPoints = 500000, IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 25, CommissionCategoryId = 3, Name = "DTR – Black Royal",   Description = "Earn $15,000/day when qualifying at Black Royal (700,000 DT points).",         Percentage = 0m, Amount = 15000m, PaymentDelayDays = 4, TeamPoints = 700000, IsActive = true, ResidualBased = true, CreationDate = SeedDate, CreatedBy = "seed" },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 4 — LEADERSHIP BONUSES
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 26, CommissionCategoryId = 4,
                Name        = "Boost Bonus – Gold",
                Description = "Earn $600 when 6+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Gold.",
                Percentage  = 0m, Amount = 600m,
                PaymentDelayDays = 15,
                IsActive = true, IsRealTime = false,
                NewMembers  = 6,
                TriggerOrder = 1,
                LifeTimeRank = 4,   // Gold = RankDefinition.SortOrder 4
                MaxEnrollmentTeamPointsPerBranch = 0.5,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 27, CommissionCategoryId = 4,
                Name        = "Boost Bonus – Platinum",
                Description = "Earn $1,200 when 12+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Platinum. Supersedes Gold if both qualify.",
                Percentage  = 0m, Amount = 1200m,
                PaymentDelayDays = 15,
                IsActive = true, IsRealTime = false,
                NewMembers  = 12,
                TriggerOrder = 2,
                LifeTimeRank = 5,   // Platinum = RankDefinition.SortOrder 5
                MaxEnrollmentTeamPointsPerBranch = 0.5,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 28, CommissionCategoryId = 4,
                Name        = "Presidential Bonus",
                Description = "Earn 20% of your Dual Team second-leg volume monthly. Unlocked at Jade rank (Lifetime Rank). Paid on the 15th.",
                Percentage  = 20m, Amount = null,
                PaymentDelayDays = 15,
                IsActive = true, IsRealTime = false,
                LifeTimeRank = 6,   // Jade = RankDefinition.SortOrder 6
                ResidualBased = false,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 5 — REVERSALS
            // Negative-amount entries used when a signup cancels within 14 days.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 29, CommissionCategoryId = 5,
                Name        = "Reversal – Member Bonus VIP",
                Description = "Reverses a VIP Member Bonus when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 30, CommissionCategoryId = 5,
                Name        = "Reversal – Member Bonus Elite",
                Description = "Reverses an Elite Member Bonus when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 31, CommissionCategoryId = 5,
                Name        = "Reversal – Member Bonus Turbo",
                Description = "Reverses a Turbo Member Bonus when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 32, CommissionCategoryId = 5,
                Name        = "Reversal – Fast Start Bonus",
                Description = "Reverses any FSB window earning when a signup cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 6 — BUILDER BONUS (Standard)
            // Additional sponsor bonus paid on top of Member Bonus to qualifying
            // ambassadors when they enroll a new paying member.
            // IsSponsorBonus = true → SponsorBonusService selects by LevelNo.
            // LevelNo matches MembershipLevel.Id: 2=VIP, 3=Elite, 4=Turbo.
            // ReverseId points to Cat 5 reversal types (IDs 41-43).
            // Default amounts — adjust per active comp plan version.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 33, CommissionCategoryId = 6,
                Name        = "Builder Bonus – VIP",
                Description = "Additional sponsor bonus paid when enrolling a VIP member. Stacks with Member Bonus.",
                Percentage  = 0m, Amount = 25m,
                PaymentDelayDays = 4,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 2,
                ReverseId = 41,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 34, CommissionCategoryId = 6,
                Name        = "Builder Bonus – Elite",
                Description = "Additional sponsor bonus paid when enrolling an Elite member. Stacks with Member Bonus.",
                Percentage  = 0m, Amount = 60m,
                PaymentDelayDays = 4,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 3,
                ReverseId = 42,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 35, CommissionCategoryId = 6,
                Name        = "Builder Bonus – Turbo",
                Description = "Superseded by Cat7 Turbo types (IDs 66-84). Deactivated.",
                Percentage  = 0m, Amount = 120m,
                PaymentDelayDays = 4,
                IsActive = false, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 4,
                ReverseId = 43,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 7 — BUILDER BONUS TURBO (Enhanced — fully separate program)
            // Elevated sponsor bonus for ambassadors enrolled in the Turbo program.
            // Completely independent set from standard Builder Bonus (Cat 6).
            // Same trigger logic but different amounts and eligibility rules.
            // ReverseId points to Cat 5 reversal types (IDs 44-46).
            // Default amounts — adjust per active comp plan version.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 36, CommissionCategoryId = 7,
                Name        = "Builder Bonus Turbo – VIP",
                Description = "Cat7 only fires for Turbo product (LevelNo=4). Deactivated for VIP.",
                Percentage  = 0m, Amount = 30m,
                PaymentDelayDays = 4,
                IsActive = false, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 2,
                ReverseId = 44,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 37, CommissionCategoryId = 7,
                Name        = "Builder Bonus Turbo – Elite",
                Description = "Cat7 only fires for Turbo product (LevelNo=4). Deactivated for Elite.",
                Percentage  = 0m, Amount = 80m,
                PaymentDelayDays = 4,
                IsActive = false, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 3,
                ReverseId = 45,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 38, CommissionCategoryId = 7,
                Name        = "Builder Bonus Turbo – Turbo",
                Description = "Builder Bonus Turbo base (rank 0 sponsors). Matches Cat6 base amount.",
                Percentage  = 0m, Amount = 60m,
                PaymentDelayDays = 4,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = true, IsSponsorBonus = true,
                LevelNo = 4,
                ReverseId = 46,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 8 — DEDUCTIONS
            // Applied as negative CommissionEarning entries by the payout engine.
            // Engine must check CommissionCategoryId == 8 to handle as deductions.
            // Admin Fee: deducted from gross commission at payout time (percentage).
            // Token Deduction: deducted in real-time per token consumed; Amount
            //   is the default unit cost — TokenType can override per token class.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 39, CommissionCategoryId = 8,
                Name        = "Admin Fee",
                Description = "Administrative fee deducted from gross commission payout. Default: 5% of payout total. Adjust via admin panel per comp plan version.",
                Percentage  = 5m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = false,
                IsPaidOnSignup = false, IsPaidOnRenewal = false,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 40, CommissionCategoryId = 8,
                Name        = "Token Deduction",
                Description = "Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; Amount here is the platform default.",
                Percentage  = 0m, Amount = 1m,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                IsPaidOnSignup = false, IsPaidOnRenewal = false,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 5 — REVERSALS (continued — Builder Bonus reversals)
            // Same 14-day chargeback rule as Member Bonus reversals (IDs 29-32).
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 41, CommissionCategoryId = 5,
                Name        = "Reversal – Builder Bonus VIP",
                Description = "Reverses a Builder Bonus VIP (ID 33) when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 42, CommissionCategoryId = 5,
                Name        = "Reversal – Builder Bonus Elite",
                Description = "Reverses a Builder Bonus Elite (ID 34) when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 43, CommissionCategoryId = 5,
                Name        = "Reversal – Builder Bonus Turbo",
                Description = "Reverses a Builder Bonus Turbo (ID 35) when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 44, CommissionCategoryId = 5,
                Name        = "Reversal – Builder Bonus Turbo VIP",
                Description = "Reverses a Builder Bonus Turbo VIP (ID 36) when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 45, CommissionCategoryId = 5,
                Name        = "Reversal – Builder Bonus Turbo Elite",
                Description = "Reverses a Builder Bonus Turbo Elite (ID 37) when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            new CommissionType
            {
                Id = 46, CommissionCategoryId = 5,
                Name        = "Reversal – Builder Bonus Turbo Turbo",
                Description = "Reverses a Builder Bonus Turbo Turbo (ID 38) when the member cancels within 14 days.",
                Percentage  = 0m, Amount = null,
                PaymentDelayDays = 0,
                IsActive = true, IsRealTime = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 6 — BUILDER BONUS ELITE (Per-Rank Tier, IDs 47-65)
            // One entry per rank. LifeTimeRank = RankDefinition.Id (= SortOrder).
            // Sponsor must hold at least that rank to receive this tier of bonus.
            // LevelNo = 3 (Elite membership product). Amounts default 0 — set via admin.
            // IsSponsorBonus = true; engine selects the highest qualifying tier.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType { Id = 47, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Silver",         Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Silver (1).",         Percentage = 0m, Amount = 10m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 1,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 48, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Gold",           Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Gold (2).",           Percentage = 0m, Amount = 20m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 2,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 49, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Platinum",       Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Platinum (3).",       Percentage = 0m, Amount = 30m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 3,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 50, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Titanium",       Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Titanium (4).",       Percentage = 0m, Amount = 40m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 4,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 51, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Jade",           Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Jade (5).",           Percentage = 0m, Amount = 50m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 5,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 52, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Pearl",          Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Pearl (6).",          Percentage = 0m, Amount = 60m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 6,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 53, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Emerald",        Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Emerald (7).",        Percentage = 0m, Amount = 65m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 7,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 54, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Ruby",           Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Ruby (8).",           Percentage = 0m, Amount = 70m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 8,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 55, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Sapphire",       Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Sapphire (9).",       Percentage = 0m, Amount = 75m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 9,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 56, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Diamond",        Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Diamond (10).",        Percentage = 0m, Amount = 80m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 10, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 57, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Double Diamond",  Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Double Diamond (11).",  Percentage = 0m, Amount = 85m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 11, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 58, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Triple Diamond",  Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Triple Diamond (12).",  Percentage = 0m, Amount = 90m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 12, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 59, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Blue Diamond",    Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Blue Diamond (13).",    Percentage = 0m, Amount = 95m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 13, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 60, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Black Diamond",   Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Black Diamond (14).",   Percentage = 0m, Amount = 100m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 14, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 61, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Royal",           Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Royal (15).",           Percentage = 0m, Amount = 105m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 15, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 62, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Double Royal",    Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Double Royal (16).",    Percentage = 0m, Amount = 110m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 16, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 63, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Triple Royal",    Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Triple Royal (17).",    Percentage = 0m, Amount = 115m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 17, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 64, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Blue Royal",      Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Blue Royal (18).",      Percentage = 0m, Amount = 120m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 18, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 65, CommissionCategoryId = 6, Name = "Builder Bonus Elite – Black Royal",     Description = "Builder Bonus for Elite enrollment. Requires lifetime rank Black Royal (19).",     Percentage = 0m, Amount = 125m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 3, LifeTimeRank = 19, CreationDate = SeedDate, CreatedBy = "seed" },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 7 — BUILDER BONUS TURBO (Per-Rank Tier, IDs 66-84)
            // One entry per rank. LifeTimeRank = RankDefinition.Id (= SortOrder).
            // LevelNo = 4 (Turbo membership product). Amounts default 0 — set via admin.
            // IsSponsorBonus = true; engine selects the highest qualifying tier.
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType { Id = 66, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Silver",         Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Silver (1).",         Percentage = 0m, Amount = 10m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 1,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 67, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Gold",           Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Gold (2).",           Percentage = 0m, Amount = 20m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 2,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 68, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Platinum",       Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Platinum (3).",       Percentage = 0m, Amount = 30m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 3,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 69, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Titanium",       Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Titanium (4).",       Percentage = 0m, Amount = 40m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 4,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 70, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Jade",           Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Jade (5).",           Percentage = 0m, Amount = 50m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 5,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 71, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Pearl",          Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Pearl (6).",          Percentage = 0m, Amount = 60m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 6,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 72, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Emerald",        Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Emerald (7).",        Percentage = 0m, Amount = 65m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 7,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 73, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Ruby",           Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Ruby (8).",           Percentage = 0m, Amount = 70m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 8,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 74, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Sapphire",       Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Sapphire (9).",       Percentage = 0m, Amount = 75m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 9,  CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 75, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Diamond",        Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Diamond (10).",        Percentage = 0m, Amount = 80m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 10, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 76, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Double Diamond",  Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Double Diamond (11).",  Percentage = 0m, Amount = 85m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 11, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 77, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Triple Diamond",  Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Triple Diamond (12).",  Percentage = 0m, Amount = 90m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 12, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 78, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Blue Diamond",    Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Blue Diamond (13).",    Percentage = 0m, Amount = 95m,  PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 13, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 79, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Black Diamond",   Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Black Diamond (14).",   Percentage = 0m, Amount = 100m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 14, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 80, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Royal",           Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Royal (15).",           Percentage = 0m, Amount = 105m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 15, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 81, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Double Royal",    Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Double Royal (16).",    Percentage = 0m, Amount = 110m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 16, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 82, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Triple Royal",    Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Triple Royal (17).",    Percentage = 0m, Amount = 115m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 17, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 83, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Blue Royal",      Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Blue Royal (18).",      Percentage = 0m, Amount = 120m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 18, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionType { Id = 84, CommissionCategoryId = 7, Name = "Builder Bonus Turbo – Black Royal",     Description = "Builder Bonus for Turbo enrollment. Requires lifetime rank Black Royal (19).",     Percentage = 0m, Amount = 125m, PaymentDelayDays = 4, IsActive = true, IsRealTime = true, IsPaidOnSignup = true, IsSponsorBonus = true, LevelNo = 4, LifeTimeRank = 19, CreationDate = SeedDate, CreatedBy = "seed" },

            // ═══════════════════════════════════════════════════════════════════
            // CATEGORY 4 — CAR BONUS (continued, ID 85)
            // Monthly lump-sum for ambassadors who maintain personal activity AND
            // accumulate sufficient Enrollment Team points in the calendar month.
            // PersonalPoints = minimum own-subscription points required (6 = Elite/Turbo active).
            // TeamPoints     = minimum cumulative ET points from downline needed.
            // Paid on the 15th of the following month (PaymentDelayDays=15).
            // ═══════════════════════════════════════════════════════════════════

            new CommissionType
            {
                Id = 85, CommissionCategoryId = 4,
                Name        = "Car Bonus",
                Description = "Earn $500/month when maintaining personal activity (6+ pts) and 1,000+ Enrollment Team points in the calendar month.",
                Percentage  = 0m, Amount = 500m,
                PaymentDelayDays = 15,
                IsActive = true, IsRealTime = false,
                PersonalPoints = 6,
                TeamPoints     = 1000,
                IsEnrollmentBased = true,
                CreationDate = SeedDate, CreatedBy = "seed"
            }
        );
    }
}
