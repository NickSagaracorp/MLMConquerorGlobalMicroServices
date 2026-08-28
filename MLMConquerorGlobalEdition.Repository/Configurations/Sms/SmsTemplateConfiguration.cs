using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Sms;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Sms;

public class SmsTemplateConfiguration : IEntityTypeConfiguration<SmsTemplate>
{
    public void Configure(EntityTypeBuilder<SmsTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.HasIndex(x => x.EventType).IsUnique();

        builder.HasMany(x => x.Localizations)
            .WithOne(x => x.SmsTemplate)
            .HasForeignKey(x => x.SmsTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
