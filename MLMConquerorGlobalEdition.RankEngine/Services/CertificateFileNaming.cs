using System.Security.Cryptography;
using System.Text;

namespace MLMConquerorGlobalEdition.RankEngine.Services;

/// <summary>
/// Builds the deterministic file name for a rank certificate PDF:
///   {sha256hex(memberGuidId)}_{memberId}_{rankSlug}.pdf
/// SHA-256 obscures the member GUID while keeping the name stable per (member, rank),
/// so regeneration overwrites the same file and deletion can recompute the name.
/// </summary>
public static class CertificateFileNaming
{
    public static string Build(string memberGuidId, string memberId, string rankName)
    {
        var hash = Sha256Hex(memberGuidId);
        var slug = Slug(rankName);
        return $"{hash}_{memberId}_{slug}.pdf";
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Rank name with every non-alphanumeric character removed ("Double Diamond" -> "DoubleDiamond").</summary>
    private static string Slug(string rankName)
        => new(rankName.Where(char.IsLetterOrDigit).ToArray());
}
