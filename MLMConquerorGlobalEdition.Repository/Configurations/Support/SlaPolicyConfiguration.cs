using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(36);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Timezone).IsRequired().HasMaxLength(100);
        builder.Property(x => x.WorkdaysJson).IsRequired().HasMaxLength(50);
        builder.Property(x => x.BusinessHoursStart).IsRequired().HasMaxLength(10);
        builder.Property(x => x.BusinessHoursEnd).IsRequired().HasMaxLength(10);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
