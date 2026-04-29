using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RankRequirementConfiguration : IEntityTypeConfiguration<RankRequirement>
{
    public void Configure(EntityTypeBuilder<RankRequirement> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SalesVolume).HasColumnType("decimal(10,2)");
        builder.Property(x => x.RankBonus).HasColumnType("decimal(10,2)");
        builder.Property(x => x.DailyBonus).HasColumnType("decimal(10,2)");
        builder.Property(x => x.MonthlyBonus).HasColumnType("decimal(10,2)");

        builder.Property(x => x.RankDescription).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CurrentRankDescription).HasMaxLength(1000);
        builder.Property(x => x.AchievementMessage).HasMaxLength(1500);
        builder.Property(x => x.CertificateUrl).HasMaxLength(500);

        builder.HasOne<RankDefinition>()
            .WithMany(r => r.Requirements)
            .HasForeignKey(x => x.RankDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ═══════════════════════════════════════════════════════════════════════
        // RANK REQUIREMENTS — full 19-rank comp plan table (canonical thresholds).
        //
        //   Silver / Gold / Platinum  → only Enrollment Team points apply.
        //                              (TeamPoints = 0, MaxTeamPointsPerBranch = 0)
        //   Titanium and above        → both Dual Team and Enrollment Team apply.
        //                              ET threshold = DT threshold / 2.
        //                              Max DT leg = 50% (each leg ≤ DT/2).
        //                              Max ET leg = 50% (each leg ≤ ET/2).
        //
        // DailyBonus  = DTR daily payout amount (corresponds to DTR CommissionType).
        // RankBonus   = one-time achievement bonus paid on first qualifying.
        // MonthlyBonus = monthly car/lifestyle bonus (0 until unlocked at Diamond+).
        // ═══════════════════════════════════════════════════════════════════════

        builder.HasData(

            new RankRequirement
            {
                Id = 1, RankDefinitionId = 1, LevelNo = 1,
                TeamPoints = 0, MaxTeamPointsPerBranch = 0,
                EnrollmentTeam = 18, MaxEnrollmentTeamPointsPerBranch = 0.66,
                SponsoredMembers = 1, PersonalPoints = 1,
                DailyBonus = 4m, RankBonus = 100m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 18 Enrollment Team points (max 66% per leg).",
                CurrentRankDescription = "You are a Silver Ambassador. Earn $4/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Silver rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 2, RankDefinitionId = 2, LevelNo = 2,
                TeamPoints = 0, MaxTeamPointsPerBranch = 0,
                EnrollmentTeam = 72, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 1, PersonalPoints = 1,
                DailyBonus = 10m, RankBonus = 300m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 72 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Gold Ambassador. Earn $10/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Gold rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 3, RankDefinitionId = 3, LevelNo = 3,
                TeamPoints = 0, MaxTeamPointsPerBranch = 0,
                EnrollmentTeam = 175, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 2, PersonalPoints = 1,
                DailyBonus = 15m, RankBonus = 500m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 175 Enrollment Team points (max 50% per leg). Boost Bonus unlocked.",
                CurrentRankDescription = "You are a Platinum Ambassador. Earn $15/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Platinum rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 4, RankDefinitionId = 4, LevelNo = 4,
                TeamPoints = 350, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 175, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 2, PersonalPoints = 1,
                DailyBonus = 25m, RankBonus = 1000m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 350 Dual Team points and 175 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Titanium Ambassador. Earn $25/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Titanium rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 5, RankDefinitionId = 5, LevelNo = 5,
                TeamPoints = 700, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 350, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 3, PersonalPoints = 1,
                DailyBonus = 40m, RankBonus = 2500m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 700 Dual Team and 350 Enrollment Team points (max 50% per leg). Presidential Bonus unlocked.",
                CurrentRankDescription = "You are a Jade Ambassador. Earn $40/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Jade rank and unlocked the Presidential Bonus!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 6, RankDefinitionId = 6, LevelNo = 6,
                TeamPoints = 1500, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 750, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 3, PersonalPoints = 1,
                DailyBonus = 80m, RankBonus = 5000m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 1,500 Dual Team and 750 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Pearl Ambassador. Earn $80/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Pearl rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 7, RankDefinitionId = 7, LevelNo = 7,
                TeamPoints = 3000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 1500, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 4, PersonalPoints = 1,
                DailyBonus = 150m, RankBonus = 10000m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 3,000 Dual Team and 1,500 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are an Emerald Ambassador. Earn $150/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Emerald rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 8, RankDefinitionId = 8, LevelNo = 8,
                TeamPoints = 6000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 3000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 5, PersonalPoints = 1,
                DailyBonus = 300m, RankBonus = 25000m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 6,000 Dual Team and 3,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Ruby Ambassador. Earn $300/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Ruby rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 9, RankDefinitionId = 9, LevelNo = 9,
                TeamPoints = 10000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 5000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 5, PersonalPoints = 1,
                DailyBonus = 500m, RankBonus = 50000m, MonthlyBonus = 0m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 10,000 Dual Team and 5,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Sapphire Ambassador. Earn $500/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Sapphire rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 10, RankDefinitionId = 10, LevelNo = 10,
                TeamPoints = 15000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 7500, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 6, PersonalPoints = 1,
                DailyBonus = 750m, RankBonus = 100000m, MonthlyBonus = 500m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 15,000 Dual Team and 7,500 Enrollment Team points (max 50% per leg). Car Bonus unlocked.",
                CurrentRankDescription = "You are a Diamond Ambassador. Earn $750/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Diamond rank and unlocked the Car Bonus!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 11, RankDefinitionId = 11, LevelNo = 11,
                TeamPoints = 20000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 10000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 6, PersonalPoints = 1,
                DailyBonus = 1000m, RankBonus = 150000m, MonthlyBonus = 750m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 20,000 Dual Team and 10,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Double Diamond Ambassador. Earn $1,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Double Diamond rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 12, RankDefinitionId = 12, LevelNo = 12,
                TeamPoints = 30000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 15000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 7, PersonalPoints = 1,
                DailyBonus = 1500m, RankBonus = 200000m, MonthlyBonus = 1000m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 30,000 Dual Team and 15,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Triple Diamond Ambassador. Earn $1,500/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Triple Diamond rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 13, RankDefinitionId = 13, LevelNo = 13,
                TeamPoints = 60000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 30000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 8, PersonalPoints = 1,
                DailyBonus = 2000m, RankBonus = 300000m, MonthlyBonus = 1500m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 60,000 Dual Team and 30,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Blue Diamond Ambassador. Earn $2,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Blue Diamond rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 14, RankDefinitionId = 14, LevelNo = 14,
                TeamPoints = 120000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 60000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 10, PersonalPoints = 1,
                DailyBonus = 3000m, RankBonus = 500000m, MonthlyBonus = 2500m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 120,000 Dual Team and 60,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Black Diamond Ambassador. Earn $3,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Black Diamond rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 15, RankDefinitionId = 15, LevelNo = 15,
                TeamPoints = 200000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 100000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 12, PersonalPoints = 1,
                DailyBonus = 4000m, RankBonus = 750000m, MonthlyBonus = 4000m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 200,000 Dual Team and 100,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Royal Ambassador. Earn $4,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Royal rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 16, RankDefinitionId = 16, LevelNo = 16,
                TeamPoints = 300000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 150000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 15, PersonalPoints = 1,
                DailyBonus = 5000m, RankBonus = 1000000m, MonthlyBonus = 5000m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 300,000 Dual Team and 150,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Double Royal Ambassador. Earn $5,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Double Royal rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 17, RankDefinitionId = 17, LevelNo = 17,
                TeamPoints = 400000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 200000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 20, PersonalPoints = 1,
                DailyBonus = 7500m, RankBonus = 1500000m, MonthlyBonus = 7500m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 400,000 Dual Team and 200,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Triple Royal Ambassador. Earn $7,500/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Triple Royal rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 18, RankDefinitionId = 18, LevelNo = 18,
                TeamPoints = 500000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 250000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 25, PersonalPoints = 1,
                DailyBonus = 10000m, RankBonus = 2000000m, MonthlyBonus = 10000m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 500,000 Dual Team and 250,000 Enrollment Team points (max 50% per leg).",
                CurrentRankDescription = "You are a Blue Royal Ambassador. Earn $10,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Blue Royal rank!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            },

            new RankRequirement
            {
                Id = 19, RankDefinitionId = 19, LevelNo = 19,
                TeamPoints = 700000, MaxTeamPointsPerBranch = 0.5,
                EnrollmentTeam = 350000, MaxEnrollmentTeamPointsPerBranch = 0.5,
                SponsoredMembers = 30, PersonalPoints = 1,
                DailyBonus = 15000m, RankBonus = 3000000m, MonthlyBonus = 15000m,
                LifetimeHoldingDuration = 0,
                RankDescription = "Qualify with 700,000 Dual Team and 350,000 Enrollment Team points (max 50% per leg). The pinnacle of the Ambassador journey.",
                CurrentRankDescription = "You are a Black Royal Ambassador. Earn $15,000/day in Dual Team Residuals.",
                AchievementMessage = "Congratulations! You have reached Black Royal — the highest rank in the company!",
                CreationDate = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "seed"
            }
        );
    }
}
