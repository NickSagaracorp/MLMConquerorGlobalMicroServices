using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

public interface IPayoutResultCsvParser
{
    WalletType GatewayType { get; }
    IReadOnlyList<PayoutResultRow> ParseResults(string csvContent);
}
