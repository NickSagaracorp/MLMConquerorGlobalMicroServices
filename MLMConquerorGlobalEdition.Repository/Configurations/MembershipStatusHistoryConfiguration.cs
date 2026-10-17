using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MembershipStatusHistoryConfiguration : IEntityTypeConfiguration<MembershipStatusHistory>
{
    public void Configure(EntityTypeBuilder<MembershipStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.SubscriptionId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => new { x.MemberId, x.ChangedAt });
        builder.HasIndex(x => x.ChangedAt);
    }
}
