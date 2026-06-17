using System.Globalization;
using System.Text;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

/// <summary>Volet bulk file. Column layout is provisional — confirm against the Volet spec.</summary>
public class VoletPayoutCsvAdapter : IPayoutCsvFormatter, IPayoutResultCsvParser
{
    public WalletType GatewayType => WalletType.Volet;

    public string FormatExport(IReadOnlyList<PayoutCsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("reference,payee_account,amount\r\n");
        foreach (var r in rows)
            sb.Append($"{r.PayoutAttemptId},{PayoutCsvParsing.CsvEscape(r.AccountSnapshot)},{r.AmountUsd.ToString("F2", CultureInfo.InvariantCulture)}\r\n");
        return sb.ToString();
    }

    public IReadOnlyList<PayoutResultRow> ParseResults(string csvContent)
        => PayoutCsvParsing.ParseStandardResults(csvContent);
}
