using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberWalletApiLogConfiguration : IEntityTypeConfiguration<MemberWalletApiLog>
{
    public void Configure(EntityTypeBuilder<MemberWalletApiLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Operation).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Endpoint).HasMaxLength(500);
        builder.Property(x => x.HttpMethod).HasMaxLength(10);
        builder.Property(x => x.RequestBody);
        builder.Property(x => x.ResponseBody);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(x => new { x.MemberId, x.WalletType });
        builder.HasIndex(x => x.CreationDate);
    }
}
