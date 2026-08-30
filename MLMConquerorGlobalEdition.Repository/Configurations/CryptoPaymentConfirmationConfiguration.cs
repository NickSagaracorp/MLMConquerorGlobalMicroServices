using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class CryptoPaymentConfirmationConfiguration : IEntityTypeConfiguration<CryptoPaymentConfirmation>
{
    public void Configure(EntityTypeBuilder<CryptoPaymentConfirmation> builder)
    {
        builder.HasKey(x => x.Id);

        // Concurrencia optimista: si dos administradores confirman la misma fila a la vez, el
        // segundo UPDATE afecta a cero filas y EF lanza DbUpdateConcurrencyException. Como las
        // comisiones se escriben en el mismo SaveChanges, el perdedor no deja nada detrás.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MemberEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.CryptoCurrency).IsRequired().HasMaxLength(20);
        builder.Property(x => x.AmountDue).HasPrecision(18, 4);
        builder.Property(x => x.CryptoTransactionId).HasMaxLength(128);
        builder.Property(x => x.ConfirmedByUserId).HasMaxLength(450);
        builder.Property(x => x.ConfirmedByEmail).HasMaxLength(256);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        // La red es la última defensa contra la doble aprobación: un pedido tiene como mucho una
        // fila de confirmación, y esa fila solo pasa de AwaitingPayment a Confirmed una vez.
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreationDate });
        builder.HasIndex(x => x.MemberId);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
