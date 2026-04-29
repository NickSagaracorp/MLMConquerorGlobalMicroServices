using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class PaymentGatewayInfoConfiguration : IEntityTypeConfiguration<PaymentGatewayInfo>
{
    public void Configure(EntityTypeBuilder<PaymentGatewayInfo> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.AdminFee).HasColumnType("decimal(10,2)");
        builder.Property(x => x.MinAdminFee).HasColumnType("decimal(10,2)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        builder.HasIndex(x => x.WalletType).IsUnique();

        // Initial info per gateway. Editable later by admin via the API.
        var seedDate = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new PaymentGatewayInfo
            {
                Id           = 1,
                WalletType   = WalletType.eWallet,
                DisplayName  = "eWallet (I-Payout)",
                Description  = "I-Payout maintains your in-account balance. Once you register, " +
                               "I-Payout sends a confirmation email; you must verify before payouts " +
                               "can be sent. Funds typically arrive within 24 hours of approval. " +
                               "International withdrawals from your I-Payout account to a bank may " +
                               "incur additional fees from I-Payout itself. " +
                               "Admin fee: $1.95 USD per transaction.",
                AdminFee     = 1.95m,
                AdminFeeKind = AdminFeeKind.Fixed,
                MinAdminFee  = null,
                Currency     = "USD",
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate
            },
            new PaymentGatewayInfo
            {
                Id           = 2,
                WalletType   = WalletType.Dwolla,
                DisplayName  = "Dwolla",
                Description  = "Dwolla pushes commissions directly into your linked US bank account. " +
                               "You must complete Dwolla's identity verification before your account " +
                               "is approved. Standard ACH transfers settle in 3–5 business days. " +
                               "Dwolla is US-only. " +
                               "Admin fee: $1.95 USD per transaction.",
                AdminFee     = 1.95m,
                AdminFeeKind = AdminFeeKind.Fixed,
                MinAdminFee  = null,
                Currency     = "USD",
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate
            },
            new PaymentGatewayInfo
            {
                Id           = 3,
                WalletType   = WalletType.Crypto,
                DisplayName  = "Crypto (Bitcoin / USDT)",
                Description  = "Provide the receiving wallet address for Bitcoin (BTC) or USDT (TRC-20). " +
                               "Double-check the address — crypto transactions are irreversible. The " +
                               "company is not liable for funds sent to a wrong address you provided. " +
                               "Network fees are deducted from the payout in addition to the admin fee. " +
                               "Admin fee: minimum 2% of payout, with a minimum of $6.95 USD per transaction.",
                AdminFee     = 2.00m,
                AdminFeeKind = AdminFeeKind.Percentage,
                MinAdminFee  = 6.95m,
                Currency     = "USD",
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate
            },
            new PaymentGatewayInfo
            {
                Id           = 4,
                WalletType   = WalletType.Advancash,
                DisplayName  = "AdvCash",
                Description  = "AdvCash holds funds in your AdvCash account in the currency of your " +
                               "choice. From there you can withdraw to bank, card, or other gateways. " +
                               "Verification through AdvCash is required to lift withdrawal limits. " +
                               "Available in most regions worldwide. " +
                               "Admin fee: $1.95 USD per transaction.",
                AdminFee     = 1.95m,
                AdminFeeKind = AdminFeeKind.Fixed,
                MinAdminFee  = null,
                Currency     = "USD",
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate
            }
        );
    }
}
