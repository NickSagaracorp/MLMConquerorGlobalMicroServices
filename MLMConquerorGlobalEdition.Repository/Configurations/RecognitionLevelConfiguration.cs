using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RecognitionLevelConfiguration : IEntityTypeConfiguration<RecognitionLevel>
{
    private static readonly DateTime SeedDate = new(2026, 11, 14);

    public void Configure(EntityTypeBuilder<RecognitionLevel> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Threshold).HasPrecision(18, 4);   // como CommissionEarning.Amount
        builder.Property(x => x.Reward).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Valores iniciales de la especificación (decisión del 2026-11-14); el admin los edita en Parameters.
        builder.HasData(
            Level(1, "100k Earner",                    50_000m,    180,  1),
            Level(2, "500k Earner",                    250_000m,   180,  2),
            Level(3, "Million Dollar Earner",          500_000m,   180,  3),
            Level(4, "100k Lifetime Earner",           100_000m,   null, 4),
            Level(5, "500k Lifetime Earner",           500_000m,   null, 5),
            Level(6, "Lifetime Million Dollar Earner", 1_000_000m, null, 6));
    }

    private static RecognitionLevel Level(int id, string name, decimal threshold, int? windowDays, int sortOrder) => new()
    {
        Id = id, Name = name, Threshold = threshold, WindowDays = windowDays, SortOrder = sortOrder, IsActive = true,
        CreationDate = SeedDate, CreatedBy = "system"
    };
}
