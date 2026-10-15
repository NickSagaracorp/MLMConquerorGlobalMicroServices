using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenType : AuditChangesIntKey
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGuestPass { get; set; }
    public string? TemplateUrl { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// ¿Se vende en el BizCenter? Decisión de negocio por tipo, no derivable de otros datos: las
    /// variantes FREE / Coupon / SpecialPromo / Help a Friend otorgan los mismos productos, con los
    /// mismos precios, que las de pago. Por defecto false: un tipo nuevo no se vende hasta que un
    /// administrador lo decide en Admin → Tokens → Token Types.
    /// </summary>
    public bool IsPurchasable { get; set; }

    /// <summary>
    /// Classification used to drive product-relation rules.
    /// See <see cref="Enums.TokenCategory"/> for semantics.
    /// </summary>
    public TokenCategory Category { get; set; } = TokenCategory.None;

    /// <summary>
    /// Product associations for this token type. Use Role to discriminate
    /// Granted (issued on redeem), UpgradeFrom (required current product),
    /// UpgradeTo (target product after upgrade).
    /// </summary>
    public ICollection<TokenTypeProduct> ProductLinks { get; set; } = new List<TokenTypeProduct>();
}
