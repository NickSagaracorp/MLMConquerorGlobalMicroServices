using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class TicketMetricsDailyConfiguration : IEntityTypeConfiguration<TicketMetricsDaily>
{
    public void Configure(EntityTypeBuilder<TicketMetricsDaily> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Date).IsUnique();
        builder.Property(x => x.TicketsByPriorityJson).HasMaxLength(2000);
        builder.Property(x => x.TicketsByCategoryJson).HasMaxLength(2000);
        builder.Property(x => x.TicketsByChannelJson).HasMaxLength(1000);
        builder.Property(x => x.TicketsByAgentJson).HasMaxLength(4000);
    }
}
