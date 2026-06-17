using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

public interface IPayoutCsvFormatter
{
    WalletType GatewayType { get; }
    string FormatExport(IReadOnlyList<PayoutCsvRow> rows);
}
