using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class RecognitionOptionConfiguration : IEntityTypeConfiguration<RecognitionOption>
{
    private static readonly DateTime SeedDate = new(2026, 11, 14);

    public void Configure(EntityTypeBuilder<RecognitionOption> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasData(
            new RecognitionOption { Id = 1, Name = "Recognized Higher Level",  SortOrder = 1, IsActive = true, CreationDate = SeedDate, CreatedBy = "system" },
            new RecognitionOption { Id = 2, Name = "Recognized Long Time Ago", SortOrder = 2, IsActive = true, CreationDate = SeedDate, CreatedBy = "system" },
            new RecognitionOption { Id = 3, Name = "Do not recognize", RequiresReason = true, SortOrder = 3, IsActive = true, CreationDate = SeedDate, CreatedBy = "system" });
    }
}
