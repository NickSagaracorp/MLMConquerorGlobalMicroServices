namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Classifies a TokenType to drive product-relation rules and validation.
/// - None       : no product relationship required (e.g., Mobile App, Annual Fee)
/// - Enrollment : token grants one or more products on signup; uses TokenTypeProduct rows with Role = Granted
/// - Upgrade    : token migrates a member from one product to another; requires exactly one UpgradeFrom + one UpgradeTo row
/// - Monthly    : recurring monthly billing token; may grant a product (Granted)
/// - Annual     : recurring annual billing token; may grant a product (Granted)
/// - Other      : misc (e.g., Travel Advantage Lite, legacy fees)
/// </summary>
public enum TokenCategory
{
    None       = 0,
    Enrollment = 1,
    Upgrade    = 2,
    Monthly    = 3,
    Annual     = 4,
    Other      = 5
}
