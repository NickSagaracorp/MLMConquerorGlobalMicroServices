using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public static class PayoutReceiptFileNaming
{
    /// <summary>Deterministic filename so verification/download can recompute it from the attempt.</summary>
    public static string Build(long payoutAttemptId, string memberId)
    {
        var hash = Sha256Hex($"{memberId}:{payoutAttemptId}");
        return $"{hash}_{memberId}_payout-{payoutAttemptId}.pdf";
    }

    public static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
