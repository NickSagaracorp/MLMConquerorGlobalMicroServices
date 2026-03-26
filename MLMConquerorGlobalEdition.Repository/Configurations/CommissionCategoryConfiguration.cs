using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CommissionCategoryConfiguration : IEntityTypeConfiguration<CommissionCategory>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<CommissionCategory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasData(
            new CommissionCategory { Id = 1, Name = "Enrollment Bonuses",       Description = "One-time bonuses paid to the direct enroller on new member signup (Member Bonus).",                      IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 2, Name = "Fast Start Bonus",         Description = "3-window FSB paid when the enroller qualifies within each countdown window.",                             IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 3, Name = "Dual Team Residuals",      Description = "Fixed daily earnings based on current binary rank qualification.",                                        IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 4, Name = "Leadership Bonuses",       Description = "Boost Bonus (weekly), Presidential Bonus (monthly), Car Bonus (monthly).",                               IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 5, Name = "Reversals",                Description = "Negative-amount entries that reverse previously paid commissions within the chargeback window.",          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 6, Name = "Builder Bonus",            Description = "Standard sponsor bonus paid on top of Member Bonus when a qualifying ambassador enrolls a new member.",  IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 7, Name = "Builder Bonus Turbo",      Description = "Enhanced sponsor bonus program with elevated payout rates, completely separate from standard Builder Bonus.", IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new CommissionCategory { Id = 8, Name = "Deductions",               Description = "Administrative fee deductions and token-related deductions applied at payout or on token consumption.",   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" }
        );
    }
}
