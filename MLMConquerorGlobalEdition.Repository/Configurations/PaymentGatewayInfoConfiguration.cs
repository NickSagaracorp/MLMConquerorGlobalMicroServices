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
        builder.Property(x => x.MinimumPayoutAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ApiVersion).HasMaxLength(10);
        builder.Property(x => x.Environment).HasMaxLength(50);
        builder.Property(x => x.AdminPortalUrl).HasMaxLength(500);

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
                MinimumPayoutAmount = 25m,
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate,
                Environment    = "Sandbox",
                AdminPortalUrl = "https://www.i-payout.com/"
            },
            new PaymentGatewayInfo
            {
                // RETIRADO (2026-08). Se conserva la fila para que el historial de payouts
                // siga resolviendo el nombre del gateway; IsActive = false la saca de la
                // selección del ambassador y del orquestador. Las wallets vivas se migraron
                // a Volet, que también es email-based.
                Id           = 2,
                WalletType   = WalletType.Dwolla,
                DisplayName  = "Dwolla (retired)",
                Description  = "Dwolla is no longer offered. Existing accounts were migrated to " +
                               "Volet, which also pays out to your email address. This entry is " +
                               "kept only so historical payout records keep resolving.",
                AdminFee     = 1.95m,
                AdminFeeKind = AdminFeeKind.Fixed,
                MinAdminFee  = null,
                Currency     = "USD",
                MinimumPayoutAmount = 25m,
                IsActive     = false,
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
                MinimumPayoutAmount = 25m,
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate
            },
            new PaymentGatewayInfo
            {
                // AdvCash se renombró a Volet. Es el MISMO proveedor, por eso la fila se
                // repunta a WalletType.Volet en vez de crear una nueva.
                Id           = 4,
                WalletType   = WalletType.Volet,
                DisplayName  = "Volet",
                Description  = "Volet (formerly AdvCash) holds funds in your Volet account in the " +
                               "currency of your choice. From there you can withdraw to bank, card, " +
                               "or other gateways. Verification through Volet is required to lift " +
                               "withdrawal limits. Available in most regions worldwide. " +
                               "Admin fee: $1.95 USD per transaction.",
                AdminFee     = 1.95m,
                AdminFeeKind = AdminFeeKind.Fixed,
                MinAdminFee  = null,
                Currency     = "USD",
                MinimumPayoutAmount = 25m,
                IsActive     = true,
                CreatedBy    = "seed",
                CreationDate = seedDate,
                AdminPortalUrl = "https://account.volet.com/"
            },
            new PaymentGatewayInfo
            {
                // Reemplaza a Dwolla en la oferta. Arranca INACTIVO: recién se habilita
                // cuando se carguen las credenciales en /admin/billing/credentials.
                // ApiVersion + Environment eligen la fila de ApiCredential a usar:
                //   ServiceKey "PayQuickerV2" + Environment "Sandbox".
                Id           = 5,
                WalletType   = WalletType.PayQuicker,
                DisplayName  = "PayQuicker",
                Description  = "PayQuicker delivers your commissions to an insured account linked " +
                               "to a debit card. You receive an invitation email to complete " +
                               "registration; once verified, funds are available instantly and can " +
                               "be spent online, at retail, or moved to your bank. " +
                               "Admin fee: $1.95 USD per transaction.",
                AdminFee     = 1.95m,
                AdminFeeKind = AdminFeeKind.Fixed,
                MinAdminFee  = null,
                Currency     = "USD",
                MinimumPayoutAmount = 25m,
                IsActive     = false,
                CreatedBy    = "seed",
                CreationDate = seedDate,
                ApiVersion     = "V2",
                Environment    = "Sandbox",
                AdminPortalUrl = "https://sandbox.payquicker.io/"
            }
        );
    }
}
