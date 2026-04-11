using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Email;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Email;

public class EmailTemplateVariableConfiguration : IEntityTypeConfiguration<EmailTemplateVariable>
{
    public void Configure(EntityTypeBuilder<EmailTemplateVariable> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Variable names are unique per template
        builder.HasIndex(x => new { x.EmailTemplateId, x.Name }).IsUnique();
    }
}
