using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TicketId).IsRequired();
        builder.Property(x => x.Field).IsRequired().HasMaxLength(100);
        builder.Property(x => x.OldValue).HasMaxLength(500);
        builder.Property(x => x.NewValue).HasMaxLength(500);
        builder.Property(x => x.ChangedByType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ChangedById).HasMaxLength(450);
        builder.Property(x => x.ChangeReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.TicketId, x.CreationDate });
    }
}
