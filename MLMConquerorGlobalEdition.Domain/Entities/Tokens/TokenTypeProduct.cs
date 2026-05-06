using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenTypeProduct : AuditChangesIntKey
{
    public int TokenTypeId { get; set; }
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Role this product plays for the parent TokenType.
    /// Granted (default) = product delivered when token is used.
    /// UpgradeFrom = required current product when redeeming an Upgrade token.
    /// UpgradeTo   = target product the member is upgraded into.
    /// </summary>
    public TokenProductRole Role { get; set; } = TokenProductRole.Granted;

    /// <summary>
    /// Quantity granted to the member when the token is redeemed.
    /// Only meaningful for Role = Granted.
    /// </summary>
    public int QuantityGranted { get; set; } = 1;
}
