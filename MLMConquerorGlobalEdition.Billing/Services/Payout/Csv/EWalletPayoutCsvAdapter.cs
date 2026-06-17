using System.Globalization;
using System.Text;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

/// <summary>i-payout (eWallet) bulk file. Column layout is provisional — confirm against the
/// i-payout bulk-upload spec when available; the Reference column carries PayoutAttempt.Id.</summary>
public class EWalletPayoutCsvAdapter : IPayoutCsvFormatter, IPayoutResultCsvParser
{
    public WalletType GatewayType => WalletType.eWallet;

    public string FormatExport(IReadOnlyList<PayoutCsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("Reference,Account,Amount,Currency\r\n");
        foreach (var r in rows)
            sb.Append($"{r.PayoutAttemptId},{PayoutCsvParsing.CsvEscape(r.AccountSnapshot)},{r.AmountUsd.ToString("F2", CultureInfo.InvariantCulture)},USD\r\n");
        return sb.ToString();
    }

    public IReadOnlyList<PayoutResultRow> ParseResults(string csvContent)
        => PayoutCsvParsing.ParseStandardResults(csvContent);
}
