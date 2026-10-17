using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M7). Sin FK: el webhook de pagos guarda filas con OrderId/MemberId vacíos si la pasarela no
/// los manda, y una FK lo haría fallar en bucle. El índice ÚNICO por (GatewayName, GatewayTransactionId)
/// está propuesto y pendiente de Nick (R7).
/// </summary>
public class PaymentHistoryConfiguration : IEntityTypeConfiguration<PaymentHistory>
{
    public void Configure(EntityTypeBuilder<PaymentHistory> builder)
    {
        builder.Property(p => p.OrderId).HasMaxLength(450).IsRequired();
        builder.Property(p => p.MemberId).HasMaxLength(100).IsRequired();
        builder.Property(p => p.GatewayTransactionId).HasMaxLength(200);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => new { p.MemberId, p.TransactionStatus }).IncludeProperties(p => p.ProcessedAt);
        builder.HasIndex(p => p.GatewayTransactionId);
    }
}
