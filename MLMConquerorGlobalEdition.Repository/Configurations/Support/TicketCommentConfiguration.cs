using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TicketId).IsRequired();
        builder.Property(x => x.AuthorId).IsRequired();
        builder.Property(x => x.AuthorType).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Body).IsRequired();

        builder.HasIndex(x => new { x.TicketId, x.CreationDate });
        builder.HasIndex(x => x.IsInternal);
    }
}
