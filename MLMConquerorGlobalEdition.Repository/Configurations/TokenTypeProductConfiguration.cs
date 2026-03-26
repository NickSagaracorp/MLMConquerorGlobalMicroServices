using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class TokenTypeProductConfiguration : IEntityTypeConfiguration<TokenTypeProduct>
{
    public void Configure(EntityTypeBuilder<TokenTypeProduct> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.TokenTypeId, x.ProductId }).IsUnique();

        builder.Property(x => x.ProductId).IsRequired().HasMaxLength(36);

        // No seed data — TokenTypeProduct rows are created when admin
        // associates products with token types via the admin panel.
    }
}
