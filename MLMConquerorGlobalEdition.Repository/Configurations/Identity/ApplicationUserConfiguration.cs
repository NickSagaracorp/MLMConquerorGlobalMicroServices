using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Identity;

/// <summary>
/// Restricciones adicionales sobre columnas propias de ApplicationUser (no las que ya
/// define IdentityDbContext). Solo acota longitud de las columnas de 2FA; no toca nada
/// que Identity ya configura.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.TwoFactorPhoneEncrypted).HasMaxLength(256);
        builder.Property(x => x.TwoFactorPhoneLast4).HasMaxLength(4);
    }
}
