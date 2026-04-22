using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Marketing;

public class MarketingDocumentConfiguration : IEntityTypeConfiguration<MarketingDocument>
{
    public void Configure(EntityTypeBuilder<MarketingDocument> builder)
    {
        builder.ToTable("MarketingDocuments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.DocumentTypeId).IsRequired();
        builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(x => x.LanguageName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.S3Key).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.OriginalFileName).HasMaxLength(500);
        builder.Property(x => x.ContentType).HasMaxLength(200);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasOne(x => x.DocumentType)
               .WithMany(x => x.Documents)
               .HasForeignKey(x => x.DocumentTypeId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DocumentTypeId);
        builder.HasIndex(x => x.LanguageCode);
        builder.HasIndex(x => new { x.DocumentTypeId, x.LanguageCode });
    }
}
