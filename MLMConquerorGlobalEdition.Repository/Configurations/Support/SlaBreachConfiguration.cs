using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class SlaBreachConfiguration : IEntityTypeConfiguration<SlaBreach>
{
    public void Configure(EntityTypeBuilder<SlaBreach> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TicketId).IsRequired();
        builder.Property(x => x.SlaPolicyId).IsRequired();
        builder.Property(x => x.AssignedAgentId).HasMaxLength(450);

        builder.HasIndex(x => x.TicketId);
        builder.HasIndex(x => x.BreachedAt);
    }
}
