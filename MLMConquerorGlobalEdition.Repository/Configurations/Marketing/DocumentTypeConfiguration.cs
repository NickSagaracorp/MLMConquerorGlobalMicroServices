using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Marketing;

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("DocumentTypes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasMany(x => x.Documents)
               .WithOne(x => x.DocumentType)
               .HasForeignKey(x => x.DocumentTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(
            new DocumentType { Id = 1, Name = "Compensation Plan",                       SortOrder = 1, IsActive = true, CreationDate = new DateTime(2026, 1, 1), CreatedBy = "system" },
            new DocumentType { Id = 2, Name = "Marketing",                               SortOrder = 2, IsActive = true, CreationDate = new DateTime(2026, 1, 1), CreatedBy = "system" },
            new DocumentType { Id = 3, Name = "Independent Lifestyle Ambassador Agreement", SortOrder = 3, IsActive = true, CreationDate = new DateTime(2026, 1, 1), CreatedBy = "system" },
            new DocumentType { Id = 4, Name = "Policies & Procedures",                   SortOrder = 4, IsActive = true, CreationDate = new DateTime(2026, 1, 1), CreatedBy = "system" }
        );
    }
}
