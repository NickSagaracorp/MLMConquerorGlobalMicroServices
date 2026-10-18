using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

/// <summary>Plan M (M6): toda unión es o.Id == od.OrderId y después od.ProductId == p.Id.</summary>
public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.Property(d => d.OrderId).HasMaxLength(450).IsRequired();
        builder.Property(d => d.ProductId).HasMaxLength(450).IsRequired();
        builder.HasIndex(d => d.OrderId).IncludeProperties(d => new { d.ProductId, d.Quantity, d.UnitPrice });
    }
}
