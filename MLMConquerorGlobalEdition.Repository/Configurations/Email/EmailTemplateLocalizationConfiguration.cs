using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Email;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Email;

public class EmailTemplateLocalizationConfiguration : IEntityTypeConfiguration<EmailTemplateLocalization>
{
    public void Configure(EntityTypeBuilder<EmailTemplateLocalization> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(500);
        builder.Property(x => x.HtmlBody).IsRequired();
        builder.Property(x => x.TextBody);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // One template can have at most one localization per language
        builder.HasIndex(x => new { x.EmailTemplateId, x.LanguageCode }).IsUnique();
    }
}
