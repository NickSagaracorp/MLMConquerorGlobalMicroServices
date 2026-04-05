using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class TicketSequenceConfiguration : IEntityTypeConfiguration<TicketSequence>
{
    public void Configure(EntityTypeBuilder<TicketSequence> builder)
    {
        builder.HasKey(x => x.Date);
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.LastSequence).IsConcurrencyToken();
    }
}
