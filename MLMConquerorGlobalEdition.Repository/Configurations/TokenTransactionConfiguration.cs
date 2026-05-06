using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(36);
        builder.Property(x => x.DistributedToMemberId).HasMaxLength(36);
        builder.Property(x => x.UsedByMemberId).HasMaxLength(36);
        builder.Property(x => x.OriginalOwnerMemberId).HasMaxLength(36);
        builder.Property(x => x.PreviousOwnerMemberId).HasMaxLength(36);
        builder.Property(x => x.UsedOnOrderId).HasMaxLength(36);
        builder.Property(x => x.ReferenceId).HasMaxLength(20);

        builder.Property(x => x.Status)
               .HasConversion<int>()
               .HasDefaultValue(TokenInstanceStatus.Issued);

        // Each redeemable instance has a unique ReferenceId (TokenCode).
        // Filtered unique index — only enforces uniqueness on rows that actually carry a code.
        builder.HasIndex(x => x.ReferenceId)
               .IsUnique()
               .HasFilter("[ReferenceId] IS NOT NULL");

        // Fast lookups for "what tokens does this member currently hold (redeemable)?"
        builder.HasIndex(x => new { x.MemberId, x.Status, x.ReferenceId });
    }
}
