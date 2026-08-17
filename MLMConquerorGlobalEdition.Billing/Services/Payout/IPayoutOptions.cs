namespace MLMConquerorGlobalEdition.Billing.Services.Payout;

/// <summary>
/// i-payout credentials for the ws_JsonAdapter.aspx endpoint, bound from the "IPayout"
/// configuration section (appsettings.json). Auth is MerchantId + Password — i-payout has no
/// separate API key. Only one environment's values are held at a time (whatever is currently
/// pasted into appsettings, sandbox today); cutting over to production means swapping these
/// values, not adding new fields. EWalletPayoutGatewayService only reads/logs these today; no
/// HTTP calls are wired yet.
/// </summary>
public class IPayoutOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
