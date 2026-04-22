using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CommissionTypeConfiguration : IEntityTypeConfiguration<CommissionType>
{
    public void Configure(EntityTypeBuilder<CommissionType> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Percentage).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Amount).HasColumnType("decimal(12,2)");
        builder.Property(x => x.AmountPromo).HasColumnType("decimal(12,2)");

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CommissionCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
