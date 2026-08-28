using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Security;

public class StepUpPolicyConfiguration : IEntityTypeConfiguration<StepUpPolicy>
{
    public void Configure(EntityTypeBuilder<StepUpPolicy> builder)
    {
        builder.HasKey(x => x.OperationKey);
        builder.Property(x => x.OperationKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);
    }
}
