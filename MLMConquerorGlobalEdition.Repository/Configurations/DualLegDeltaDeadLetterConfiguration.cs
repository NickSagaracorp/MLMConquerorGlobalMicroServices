using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class DualLegDeltaDeadLetterConfiguration : IEntityTypeConfiguration<DualLegDeltaDeadLetter>
{
    public void Configure(EntityTypeBuilder<DualLegDeltaDeadLetter> builder)
    {
        builder.ToTable("DualLegDeltaDeadLetters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartMemberId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Points).HasPrecision(18, 4);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
        builder.HasIndex(x => x.StartMemberId);
    }
}
