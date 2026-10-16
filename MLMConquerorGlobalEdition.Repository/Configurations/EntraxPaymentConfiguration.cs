using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class EntraxPaymentConfiguration : IEntityTypeConfiguration<EntraxPayment>
{
    public void Configure(EntityTypeBuilder<EntraxPayment> builder)
    {
        builder.HasKey(x => x.Id);

        // La carrera entre webhook, pantalla, barrido y admin la decide esta columna: el segundo
        // UPDATE de la misma fila afecta a cero filas y EF lanza DbUpdateConcurrencyException.
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.OrderId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ExternalOrderId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ExternalPaymentId).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.AmountUsd).HasPrecision(18, 4);
        builder.Property(x => x.AmountRub).HasPrecision(18, 4);
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        builder.Property(x => x.PaidAmountUsd).HasPrecision(18, 4);
        builder.Property(x => x.QrLink).HasMaxLength(1000);
        builder.Property(x => x.FailureCode).HasMaxLength(60);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastUpdateBy).HasMaxLength(100);

        // Un orderId de Entrax es de un solo intento; un paymentId de Entrax, de un solo alta.
        builder.HasIndex(x => x.ExternalOrderId).IsUnique();
        builder.HasIndex(x => x.ExternalPaymentId).IsUnique().HasFilter("[ExternalPaymentId] IS NOT NULL");

        builder.HasIndex(x => x.OrderId);

        // Un pedido lo activa como mucho UN pago de Entrax: dos QR distintos del mismo alta (uno pagado
        // tarde y el siguiente) no pueden quedar los dos Consumed. Es la red debajo de la transición
        // atómica del pedido (SignupOrderClaim): si aun así llegaran dos, el segundo guardado choca aquí.
        builder.HasIndex(x => x.OrderId, "UX_EntraxPayments_OrderId_Consumed")
               .IsUnique()
               .HasFilter("[Consumed] = 1");
        builder.HasIndex(x => new { x.Status, x.CreationDate });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
