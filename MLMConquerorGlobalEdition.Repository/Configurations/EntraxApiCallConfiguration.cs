using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class EntraxApiCallConfiguration : IEntityTypeConfiguration<EntraxApiCall>
{
    public void Configure(EntityTypeBuilder<EntraxApiCall> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Operation).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ExternalOrderId).HasMaxLength(100);
        builder.Property(x => x.ExternalPaymentId).HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasMaxLength(60);
        builder.Property(x => x.ProviderStatus).HasMaxLength(30);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.CreationDate);
        builder.HasIndex(x => x.ExternalPaymentId);
    }
}
