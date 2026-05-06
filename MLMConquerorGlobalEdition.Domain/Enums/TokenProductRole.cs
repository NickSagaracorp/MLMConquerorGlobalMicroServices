namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Role a product plays inside a TokenType ↔ Product association.
/// - Granted     : product is delivered to the member when the token is redeemed
/// - UpgradeFrom : member must currently own this product to redeem an Upgrade token
/// - UpgradeTo   : product the member is upgraded INTO when the Upgrade token is redeemed
/// </summary>
public enum TokenProductRole
{
    Granted     = 0,
    UpgradeFrom = 1,
    UpgradeTo   = 2
}
