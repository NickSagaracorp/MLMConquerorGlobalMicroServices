using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class SupportAgentConfiguration : IEntityTypeConfiguration<SupportAgent>
{
    public void Configure(EntityTypeBuilder<SupportAgent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.UserId).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SkillsJson).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.LanguagesJson).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Availability).IsRequired().HasMaxLength(20);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
