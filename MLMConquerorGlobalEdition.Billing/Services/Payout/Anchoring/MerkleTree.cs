using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;

public static class MerkleTree
{
    /// <summary>Computes a Merkle root (hex) over the ordered leaf hashes. Odd nodes are duplicated.
    /// Empty input returns the SHA-256 of an empty string.</summary>
    public static string ComputeRoot(IReadOnlyList<string> leafHashesHex)
    {
        if (leafHashesHex.Count == 0) return Sha256Hex(string.Empty);

        var level = leafHashesHex.Select(h => h.ToLowerInvariant()).ToList();
        while (level.Count > 1)
        {
            var next = new List<string>();
            for (var i = 0; i < level.Count; i += 2)
            {
                var left = level[i];
                var right = (i + 1 < level.Count) ? level[i + 1] : level[i]; // duplicate last if odd
                next.Add(Sha256Hex(left + right));
            }
            level = next;
        }
        return level[0];
    }

    public static string Sha256Hex(string input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();

    /// <summary>The running chain hash for the next receipt: SHA-256(prevChainHash + thisReceiptSha256).</summary>
    public static string ChainHash(string prevChainHash, string thisSha256)
        => Sha256Hex(prevChainHash + thisSha256);

    public const string Genesis = "GENESIS";
}
