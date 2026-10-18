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
    }
}
