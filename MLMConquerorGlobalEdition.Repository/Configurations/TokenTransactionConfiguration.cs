using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });
    }
}
