namespace MLMConquerorGlobalEdition.BizCenter.Services.Billing;

/// <summary>
/// Tokenizes raw card data with the configured payment gateway. The real
/// implementation calls Spreedly or Stripe; in dev there is no sandbox so
/// <see cref="SimulatedCardTokenizationService"/> returns a fake token.
/// </summary>
public interface ICardTokenizationService
{
    Task<TokenizationResult> TokenizeAsync(
        string rawCardNumber,
        int    expiryMonth,
        int    expiryYear,
        string cardholderName,
        string cvv,
        CancellationToken ct = default);

    /// <summary>Detects card brand from the leading digits (BIN).</summary>
    string DetectBrand(string rawCardNumber);
}

public record TokenizationResult(
    string Gateway,
    string GatewayToken,
    string CardToken);
