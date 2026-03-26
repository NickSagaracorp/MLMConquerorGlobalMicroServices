using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Full 19-rank structure from the MWR Life Compensation Plan.
/// SortOrder is used by:
///   - CommissionType.LifeTimeRank (minimum qualifying rank for Presidential/Boost)
///   - MemberRankHistory (tracking lifetime/current rank)
/// Silver/Gold/Platinum qualify via Enrollment Team points.
/// Titanium through Black Royal qualify via Dual Team points.
/// </summary>
public class RankDefinitionConfiguration : IEntityTypeConfiguration<RankDefinition>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<RankDefinition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CertificateTemplateUrl).HasMaxLength(1000);

        builder.HasData(
            new RankDefinition { Id = 1,  Name = "Silver",         Description = "18 ET points (3 Elite/Turbo members). DTR: $4/day.",        SortOrder = 1,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 2,  Name = "Gold",           Description = "72 ET points (12 Elite/Turbo members). DTR: $10/day.",       SortOrder = 2,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 3,  Name = "Platinum",       Description = "175 ET points. DTR: $15/day. Boost Bonus unlocked.",         SortOrder = 3,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 4,  Name = "Titanium",       Description = "350 DT / 175 ET points. DTR: $25/day.",                      SortOrder = 4,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 5,  Name = "Jade",           Description = "700 DT / 350 ET points. DTR: $40/day. Presidential unlocked.", SortOrder = 5, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 6,  Name = "Pearl",          Description = "1,500 DT / 750 ET points. DTR: $80/day.",                    SortOrder = 6,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 7,  Name = "Emerald",        Description = "3,000 DT / 1,500 ET points. DTR: $150/day.",                 SortOrder = 7,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 8,  Name = "Ruby",           Description = "6,000 DT / 3,000 ET points. DTR: $300/day.",                 SortOrder = 8,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 9,  Name = "Sapphire",       Description = "10,000 DT / 5,000 ET points. DTR: $500/day.",                SortOrder = 9,  Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 10, Name = "Diamond",        Description = "15,000 DT / 7,500 ET points. DTR: $750/day.",                SortOrder = 10, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 11, Name = "Double Diamond", Description = "20,000 DT / 10,000 ET points. DTR: $1,000/day.",             SortOrder = 11, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 12, Name = "Triple Diamond", Description = "30,000 DT / 15,000 ET points. DTR: $1,500/day.",             SortOrder = 12, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 13, Name = "Blue Diamond",   Description = "60,000 DT / 30,000 ET points. DTR: $2,000/day.",             SortOrder = 13, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 14, Name = "Black Diamond",  Description = "120,000 DT / 60,000 ET points. DTR: $3,000/day.",            SortOrder = 14, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 15, Name = "Royal",          Description = "200,000 DT / 100,000 ET points. DTR: $4,000/day.",           SortOrder = 15, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 16, Name = "Double Royal",   Description = "300,000 DT / 150,000 ET points. DTR: $5,000/day.",           SortOrder = 16, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 17, Name = "Triple Royal",   Description = "400,000 DT / 200,000 ET points. DTR: $7,500/day.",           SortOrder = 17, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 18, Name = "Blue Royal",     Description = "500,000 DT / 250,000 ET points. DTR: $10,000/day.",          SortOrder = 18, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" },
            new RankDefinition { Id = 19, Name = "Black Royal",    Description = "700,000 DT / 350,000 ET points. DTR: $15,000/day.",          SortOrder = 19, Status = RankDefinitionStatus.Active, CreationDate = SeedDate, CreatedBy = "seed" }
        );
    }
}
