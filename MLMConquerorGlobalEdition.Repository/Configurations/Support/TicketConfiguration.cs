using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.TicketNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.TicketNumber).IsUnique();

        builder.Property(x => x.MemberId).IsRequired();
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.Subcategory).HasMaxLength(100);
        builder.Property(x => x.Language).HasMaxLength(10);
        builder.Property(x => x.CustomerTier).HasMaxLength(50);
        builder.Property(x => x.ResolutionSummary).HasMaxLength(2000);
        builder.Property(x => x.ResolvedByAgentId).HasMaxLength(450);
        builder.Property(x => x.CsatComment).HasMaxLength(1000);
        builder.Property(x => x.MergedIntoTicketId).HasMaxLength(36);

        // Indexes
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
        builder.HasIndex(x => new { x.AssignedToUserId, x.Status });
        builder.HasIndex(x => new { x.Status, x.LastUpdateDate });
        builder.HasIndex(x => new { x.AssignedTeamId, x.Status });

        // Relationships
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SlaPolicy)
            .WithMany()
            .HasForeignKey(x => x.SlaPolicyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AssignedTeam)
            .WithMany()
            .HasForeignKey(x => x.AssignedTeamId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.History)
            .WithOne(h => h.Ticket)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.SlaBreaches)
            .WithOne(b => b.Ticket)
            .HasForeignKey(b => b.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
