using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Configurations.General;

public class GlobalParameterConfiguration : IEntityTypeConfiguration<GlobalParameter>
{
    public void Configure(EntityTypeBuilder<GlobalParameter> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Value).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.Key).IsUnique();
    }
}
