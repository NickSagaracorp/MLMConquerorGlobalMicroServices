using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

/// <summary>
/// Pure-domain helper that validates an Upgrade token redemption against the
/// member's current product and the requested target product.
///
/// Rules:
///  - Token must be Category = Upgrade.
///  - Among the token's product links, exactly one must have Role = UpgradeFrom
///    matching <paramref name="memberCurrentProductId"/>.
///  - Among the token's product links, exactly one must have Role = UpgradeTo
///    matching <paramref name="requestedTargetProductId"/>.
/// </summary>
public static class TokenTypeUpgradeValidator
{
    public static void ValidateUpgradePath(
        TokenType tokenType,
        IReadOnlyCollection<TokenTypeProduct> productLinks,
        string memberCurrentProductId,
        string requestedTargetProductId)
    {
        if (tokenType is null)
            throw new ArgumentNullException(nameof(tokenType));
        if (productLinks is null)
            throw new ArgumentNullException(nameof(productLinks));

        if (tokenType.Category != TokenCategory.Upgrade)
        {
            throw new InvalidUpgradeTokenPathException(
                $"Token '{tokenType.Name}' is not an Upgrade token (Category={tokenType.Category}).");
        }

        var fromLinks = productLinks.Where(p => p.Role == TokenProductRole.UpgradeFrom).ToList();
        var toLinks   = productLinks.Where(p => p.Role == TokenProductRole.UpgradeTo).ToList();

        if (fromLinks.Count != 1 || toLinks.Count != 1)
        {
            throw new InvalidUpgradeTokenPathException(
                $"Upgrade token '{tokenType.Name}' must define exactly one UpgradeFrom and one UpgradeTo product link " +
                $"(found {fromLinks.Count} from / {toLinks.Count} to).");
        }

        if (!string.Equals(fromLinks[0].ProductId, memberCurrentProductId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidUpgradeTokenPathException(
                $"Token requires current product '{fromLinks[0].ProductId}' but member currently holds '{memberCurrentProductId}'.");
        }

        if (!string.Equals(toLinks[0].ProductId, requestedTargetProductId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidUpgradeTokenPathException(
                $"Token upgrades to '{toLinks[0].ProductId}' but request targets '{requestedTargetProductId}'.");
        }
    }
}
