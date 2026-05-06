using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;

public class TokenTypeProductDto
{
    public int Id { get; set; }
    public int TokenTypeId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public TokenProductRole Role { get; set; }
    public int QuantityGranted { get; set; }
}

public class TokenTypeProductsPayloadDto
{
    /// <summary>Products granted when the token is redeemed (Role = Granted).</summary>
    public List<string> GrantedProductIds { get; set; } = new();

    /// <summary>Source product the member must currently own to redeem an Upgrade token.</summary>
    public string? UpgradeFromProductId { get; set; }

    /// <summary>Target product the member is upgraded INTO when an Upgrade token is redeemed.</summary>
    public string? UpgradeToProductId { get; set; }
}
