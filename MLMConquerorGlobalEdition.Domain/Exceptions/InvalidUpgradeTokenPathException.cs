namespace MLMConquerorGlobalEdition.Domain.Exceptions;

/// <summary>
/// Thrown when an Upgrade token is redeemed but its UpgradeFrom/UpgradeTo product
/// mappings do not match the member's current product or the requested target product.
/// Prevents misuse of valid-but-wrong upgrade tokens.
/// </summary>
public class InvalidUpgradeTokenPathException : DomainException
{
    public InvalidUpgradeTokenPathException(string detail)
        : base("INVALID_UPGRADE_TOKEN_PATH",
              $"This upgrade token is not valid for the requested product change. {detail}") { }
}
