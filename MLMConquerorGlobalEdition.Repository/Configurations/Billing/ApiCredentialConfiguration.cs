using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Billing;

public class ApiCredentialConfiguration : IEntityTypeConfiguration<ApiCredential>
{
    public void Configure(EntityTypeBuilder<ApiCredential> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.ServiceKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Environment).IsRequired().HasMaxLength(50);
        builder.Property(x => x.BaseUrl).HasMaxLength(500);
        builder.Property(x => x.ApiKeyEncrypted).HasMaxLength(2000);
        builder.Property(x => x.SecretKeyEncrypted).HasMaxLength(2000);
        builder.Property(x => x.MerchantIdEncrypted).HasMaxLength(2000);
        builder.Property(x => x.AdditionalSecretEncrypted).HasMaxLength(2000);
        builder.Property(x => x.SpreedlyGatewayTokenEncrypted).HasMaxLength(2000);
        builder.Property(x => x.PortalUrl).HasMaxLength(500);
        builder.Property(x => x.PortalUsernameEncrypted).HasMaxLength(2000);
        builder.Property(x => x.PortalPasswordEncrypted).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.ServiceKey, x.Environment }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
