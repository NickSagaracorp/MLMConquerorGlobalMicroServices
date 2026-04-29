using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Services.Wallets;

/// <summary>
/// Single source of truth for wallet payloads, shared by BizCenter (member-self
/// view) and Admin (admin viewing a member's wallets).
/// </summary>
public class WalletAccountView
{
    public string       WalletId          { get; set; } = string.Empty;
    public string       MemberId          { get; set; } = string.Empty;
    public WalletType   WalletType        { get; set; }
    public string       WalletTypeName    { get; set; } = string.Empty;
    public WalletStatus Status            { get; set; }
    public string       StatusName        { get; set; } = string.Empty;
    public string?      AccountIdentifier { get; set; }
    public bool         IsPreferred       { get; set; }
    public string?      Notes             { get; set; }
    public DateTime     CreationDate      { get; set; }
    public DateTime     LastUpdateDate    { get; set; }
}

public class WalletHistoryView
{
    public long         Id                   { get; set; }
    public string       WalletId             { get; set; } = string.Empty;
    public WalletType   WalletType           { get; set; }
    public string       Action               { get; set; } = string.Empty;
    public WalletStatus OldStatus            { get; set; }
    public WalletStatus NewStatus            { get; set; }
    public string?      OldAccountIdentifier { get; set; }
    public string?      NewAccountIdentifier { get; set; }
    public bool?        OldIsPreferred       { get; set; }
    public bool?        NewIsPreferred       { get; set; }
    public string?      ChangeReason         { get; set; }
    public string       ChangedBy            { get; set; } = string.Empty;
    public DateTime     OccurredAt           { get; set; }
}

public class WalletApiLogView
{
    public long       Id           { get; set; }
    public WalletType WalletType   { get; set; }
    public string     Operation    { get; set; } = string.Empty;
    public string?    Endpoint     { get; set; }
    public string?    HttpMethod   { get; set; }
    public string?    RequestBody  { get; set; }
    public int        HttpStatusCode { get; set; }
    public string?    ResponseBody { get; set; }
    public bool       Success      { get; set; }
    public string?    ErrorMessage { get; set; }
    public int        DurationMs   { get; set; }
    public DateTime   OccurredAt   { get; set; }
}

public class SaveWalletRequest
{
    public WalletType WalletType        { get; set; }
    public string     AccountIdentifier { get; set; } = string.Empty;
    public string?    Notes             { get; set; }
    /// <summary>For eWallet only — encrypted password (must be prefixed "ENC:").</summary>
    public string?    EWalletPasswordEncrypted { get; set; }
}

public class PaymentGatewayInfoView
{
    public WalletType WalletType   { get; set; }
    public string     WalletTypeName { get; set; } = string.Empty;
    public string     DisplayName  { get; set; } = string.Empty;
    public string     Description  { get; set; } = string.Empty;
    public decimal    AdminFee     { get; set; }
    public string     AdminFeeKind { get; set; } = "Fixed";
    /// <summary>Optional minimum fee (Percentage gateways only).</summary>
    public decimal?   MinAdminFee  { get; set; }
    /// <summary>Pre-formatted display string e.g. "$1.95" or "2.00% (min $6.95)".</summary>
    public string     AdminFeeDisplay { get; set; } = string.Empty;
    public string     Currency     { get; set; } = "USD";
    public bool       IsActive     { get; set; }
}
