using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Marketing;

public class S3StorageConfigConfiguration : IEntityTypeConfiguration<S3StorageConfig>
{
    public void Configure(EntityTypeBuilder<S3StorageConfig> builder)
    {
        builder.ToTable("S3StorageConfig");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BucketName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Region).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FolderPrefix).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);
    }
}
