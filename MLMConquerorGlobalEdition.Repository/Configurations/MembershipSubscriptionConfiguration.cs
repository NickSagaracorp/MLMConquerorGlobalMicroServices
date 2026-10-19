using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M8): la navegación LastOrder creaba una FK en sombra (LastOrderId1) vacía en todas las
/// filas. La relación va sobre LastOrderId, que es la columna que se rellena.
/// </summary>
public class MembershipSubscriptionConfiguration : IEntityTypeConfiguration<MembershipSubscription>
{
    public void Configure(EntityTypeBuilder<MembershipSubscription> builder)
    {
        builder.Property(s => s.LastOrderId).HasMaxLength(450);
        builder.HasOne(s => s.LastOrder)
            .WithMany()
            .HasForeignKey(s => s.LastOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Plan Q: las suscripciones activas por miembro, para unir el equipo (millones de filas) con ellas sin leer la tabla.
        // Solo el estado: las consultas no filtran IsDeleted (la entidad no tiene filtro global), y un filtro con
        // IsDeleted dejaria el indice sin uso.
        // El indice de la FK (IX_MembershipSubscriptions_MemberId) se declara aqui: si no, EF lo da por cubierto con el
        // filtrado y lo borra, y las consultas por miembro sin estado se quedarian sin indice.
        builder.HasIndex(s => s.MemberId);
        builder.HasIndex(new[] { nameof(MembershipSubscription.MemberId) }, "IX_MembershipSubscriptions_Activas")
            .HasFilter("[SubscriptionStatus] = 1");
    }
}
