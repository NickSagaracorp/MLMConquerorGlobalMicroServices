using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class SupportTeamConfiguration : IEntityTypeConfiguration<SupportTeam>
{
    public void Configure(EntityTypeBuilder<SupportTeam> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.SupervisorAgentId).HasMaxLength(450);
        builder.Property(x => x.RoutingMethod).IsRequired().HasMaxLength(30);

        builder.HasMany(x => x.Agents)
            .WithOne(a => a.Team)
            .HasForeignKey(a => a.TeamId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
