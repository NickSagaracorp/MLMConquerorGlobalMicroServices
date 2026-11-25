using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// La navegación LastOrder creaba una FK en sombra (LastOrderId1) vacía en todas las
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

        // Sin cascada desde el nivel. Borrar un nivel borraba sus suscripciones en
        // SQL, por debajo del interceptor. El admin desactiva niveles (IsActive), no los borra.
        builder.HasOne(s => s.MembershipLevel)
            .WithMany()
            .HasForeignKey(s => s.MembershipLevelId)
            .OnDelete(DeleteBehavior.NoAction);

        // Las suscripciones activas por miembro, para unir el equipo (millones de filas) con ellas sin leer la tabla.
        // Solo el estado: las consultas no filtran IsDeleted (la entidad no tiene filtro global), y un filtro con
        // IsDeleted dejaria el indice sin uso.
        // El indice de la FK (IX_MembershipSubscriptions_MemberId) se declara aqui: si no, EF lo da por cubierto con el
        // filtrado y lo borra, y las consultas por miembro sin estado se quedarian sin indice.
        builder.HasIndex(s => s.MemberId);
        // Grid de Subscriptions del admin: lo más nuevo primero sin ordenar el millón, y los contadores por estado
        // ("por vencer": activas con EndDate cercana) sin recorrer la tabla.
        builder.HasIndex(s => s.CreationDate).HasDatabaseName("IX_MembershipSubscriptions_CreationDate").IsDescending();
        builder.HasIndex(s => new { s.SubscriptionStatus, s.EndDate }).HasDatabaseName("IX_MembershipSubscriptions_Status_EndDate");
        builder.HasIndex(new[] { nameof(MembershipSubscription.MemberId) }, "IX_MembershipSubscriptions_Activas")
            .HasFilter("[SubscriptionStatus] = 1");
    }
}
