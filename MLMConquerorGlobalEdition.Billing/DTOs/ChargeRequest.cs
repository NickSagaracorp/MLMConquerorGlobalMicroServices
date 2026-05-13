using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.DTOs;

public class ChargeRequest
{
    public string MemberId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional admin override: if set, bypass the router and charge this gateway directly.
    /// For legacy wallet/payout path, use WalletType separately.
    /// </summary>
    public WalletType? Gateway { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Optional: attach charge to an existing order.</summary>
    public string? OrderId { get; set; }

    // ── Card routing fields (new) ─────────────────────────────────────────

    /// <summary>The type of billing operation — required for routing.</summary>
    public BillingOperationType OperationType { get; set; } = BillingOperationType.Payment;

    /// <summary>Detected or supplied card brand. When null, detector runs on First6.</summary>
    public CardBrand? CardBrand { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country of the cardholder.</summary>
    public string CardholderCountryIso { get; set; } = "US";

    /// <summary>Tokenized card reference (gateway/Spreedly token).</summary>
    public string? TokenizedCardRef { get; set; }

    /// <summary>Network transaction ID from the initial authorization (recurring compliance).</summary>
    public string? NetworkTransactionId { get; set; }

    /// <summary>First 6 digits of the PAN for BIN-based brand detection (when CardBrand not supplied).</summary>
    public string? CardFirst6 { get; set; }

    /// <summary>Optional admin override to force a specific CardProcessor (bypasses router).</summary>
    public CardProcessor? ProcessorOverride { get; set; }
}
