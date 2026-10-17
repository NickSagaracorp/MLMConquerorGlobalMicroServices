using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CorporateEventRegistrationConfiguration : IEntityTypeConfiguration<CorporateEventRegistration>
{
    public void Configure(EntityTypeBuilder<CorporateEventRegistration> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventId).IsRequired().HasMaxLength(450);
        // La misma longitud que MemberProfiles.MemberId, la columna a la que apunta la clave foránea:
        // si MemberId crece (en el cliente hay ids de 50), esta tiene que crecer con ella.
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ExternalRef).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasOne<CorporateEvent>().WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberProfile>().WithMany()
            .HasForeignKey(x => x.MemberId)
            .HasPrincipalKey(m => m.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // La idempotencia de la futura importación la garantizará la base: una compra (referencia
        // externa) entra una sola vez por evento, aunque se importe el mismo archivo dos veces a la vez.
        builder.HasIndex(x => new { x.EventId, x.ExternalRef }).IsUnique();
        builder.HasIndex(x => new { x.EventId, x.MemberId });
    }
}
