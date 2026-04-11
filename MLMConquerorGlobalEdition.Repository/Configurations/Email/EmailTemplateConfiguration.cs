using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Email;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Email;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.EventType);

        builder.HasMany(x => x.Localizations)
            .WithOne(x => x.EmailTemplate)
            .HasForeignKey(x => x.EmailTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Variables)
            .WithOne(x => x.EmailTemplate)
            .HasForeignKey(x => x.EmailTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
