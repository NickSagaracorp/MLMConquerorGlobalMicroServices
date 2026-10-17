using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.SharedKernel;
using OrdersEntity = MLMConquerorGlobalEdition.Domain.Entities.Orders.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>
/// Plan M (M1–M6). Id se queda en nvarchar(450) (es la PK: acortarla no gana nada y obligaría a
/// recrear la PK y sus FK). Cada índice sale de una consulta real; ver el plan.
/// </summary>
public class OrdersConfiguration : IEntityTypeConfiguration<OrdersEntity>
{
    public void Configure(EntityTypeBuilder<OrdersEntity> builder)
    {
        builder.Property(o => o.MemberId).HasMaxLength(20).IsRequired();
        builder.Property(o => o.OrderNo).HasMaxLength(OrderNumberHelper.MaxLength);
        builder.Property(o => o.MembershipSubscriptionId).HasMaxLength(450);

        // Los seis bucles de unicidad (AnyAsync) pasan de recorrer la tabla a un seek.
        builder.HasIndex(o => o.OrderNo).IsUnique().HasFilter("[OrderNo] IS NOT NULL");
        // Orden de alta (Actividad, SignupEnrollmentOrder), historial de facturación, rangos, bonos.
        builder.HasIndex(o => new { o.MemberId, o.OrderDate })
            .IncludeProperties(o => new { o.Status, o.CreationDate, o.IsDeleted });
        // Pedidos de una suscripción (Admin) y unión del panel CEO.
        builder.HasIndex(o => o.MembershipSubscriptionId);
        // Paneles CEO y financiero, estadísticas de promociones: rangos de fecha.
        builder.HasIndex(o => o.OrderDate).IncludeProperties(o => new { o.Status, o.TotalAmount });

        builder.HasMany(o => o.OrderDetails)
            .WithOne()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
