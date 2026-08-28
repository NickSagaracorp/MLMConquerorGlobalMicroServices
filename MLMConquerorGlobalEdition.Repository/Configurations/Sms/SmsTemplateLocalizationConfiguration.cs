using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Sms;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Sms;

public class SmsTemplateLocalizationConfiguration : IEntityTypeConfiguration<SmsTemplateLocalization>
{
    public void Configure(EntityTypeBuilder<SmsTemplateLocalization> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(480);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => new { x.SmsTemplateId, x.LanguageCode }).IsUnique();

        builder.HasOne(x => x.SmsTemplate)
            .WithMany(x => x.Localizations)
            .HasForeignKey(x => x.SmsTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
