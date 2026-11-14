using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberRecognitionConfiguration : IEntityTypeConfiguration<MemberRecognition>
{
    public void Configure(EntityTypeBuilder<MemberRecognition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);   // como MemberProfiles.MemberId
        builder.Property(x => x.ReachedDate).HasColumnType("date");
        builder.Property(x => x.QualifyingTotal).HasPrecision(18, 4);
        builder.Property(x => x.CeoCallMonth).HasColumnType("date");
        builder.Property(x => x.CeoCallReason).HasMaxLength(300);
        builder.Property(x => x.EventReason).HasMaxLength(300);
        builder.Property(x => x.RewardSize).HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Una fila por Ambassador y nivel: la detección (nocturna o histórica) es idempotente contra este índice.
        builder.HasIndex(x => new { x.MemberId, x.RecognitionLevelId })
               .IsUnique()
               .HasDatabaseName("UX_MemberRecognitions_Member_Level");
        builder.HasIndex(x => new { x.RecognitionLevelId, x.ReachedDate });

        builder.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.RecognitionLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CeoCallOption).WithMany().HasForeignKey(x => x.CeoCallOptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EventOption).WithMany().HasForeignKey(x => x.EventOptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
