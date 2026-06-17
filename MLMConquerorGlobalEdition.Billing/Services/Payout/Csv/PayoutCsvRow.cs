namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Csv;

/// <summary>An export line. Reference == PayoutAttempt.Id so the results file maps back deterministically.</summary>
public record PayoutCsvRow(long PayoutAttemptId, string MemberId, string AccountSnapshot, decimal AmountUsd);

/// <summary>A parsed result line from the gateway's returned file.</summary>
public record PayoutResultRow(long PayoutAttemptId, bool Success, string? GatewayTransactionId, string? ErrorCode, string? ErrorMessage);

internal static class PayoutCsvParsing
{
    /// <summary>Centralised CSV escaping used by all adapters. Guards commas, double-quotes, and newlines.</summary>
    internal static string CsvEscape(string v) => v.Contains(',') || v.Contains('"') || v.Contains('\n')
        ? $"\"{v.Replace("\"", "\"\"")}\"" : v;


    /// <summary>Parses a results file with header columns:
    /// Reference, Status, TransactionId, ErrorCode, ErrorMessage (case-insensitive; Status SUCCESS/OK/PAID = success).</summary>
    public static IReadOnlyList<PayoutResultRow> ParseStandardResults(string csvContent)
    {
        var rows = new List<PayoutResultRow>();
        var lines = csvContent.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < lines.Length; i++) // skip header
        {
            var cols = SplitCsvLine(lines[i]);
            if (cols.Count < 2 || !long.TryParse(cols[0].Trim(), out var attemptId)) continue;
            var status = cols[1].Trim().ToUpperInvariant();
            var success = status is "SUCCESS" or "OK" or "PAID" or "COMPLETED";
            rows.Add(new PayoutResultRow(
                attemptId, success,
                cols.Count > 2 ? Empty(cols[2]) : null,
                cols.Count > 3 ? Empty(cols[3]) : null,
                cols.Count > 4 ? Empty(cols[4]) : null));
        }
        return rows;
    }

    private static string? Empty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return result;
    }
}
