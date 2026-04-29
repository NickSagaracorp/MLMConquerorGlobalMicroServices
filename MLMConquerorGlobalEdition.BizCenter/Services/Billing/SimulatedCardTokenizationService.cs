using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.BizCenter.Services.Billing;

/// <summary>
/// Stand-in for the real Spreedly / Stripe tokenization API. Generates plausible
/// fake tokens so the rest of the flow (storing card, charging recurring fees)
/// can be exercised end-to-end. Replace with the real client once the production
/// gateway sandbox credentials are available.
/// </summary>
public class SimulatedCardTokenizationService : ICardTokenizationService
{
    public Task<TokenizationResult> TokenizeAsync(
        string rawCardNumber,
        int    expiryMonth,
        int    expiryYear,
        string cardholderName,
        string cvv,
        CancellationToken ct = default)
    {
        // Simulated network round-trip; gives the spinner something to show.
        return Task.FromResult(new TokenizationResult(
            Gateway:      "spreedly-simulated",
            GatewayToken: "tok_" + Guid.NewGuid().ToString("N")[..24],
            CardToken:    "card_" + Guid.NewGuid().ToString("N")));
    }

    /// <summary>Brand detection by BIN — covers the major networks we accept.</summary>
    public string DetectBrand(string rawCardNumber)
    {
        var digits = OnlyDigits(rawCardNumber);
        if (digits.Length < 1) return "Unknown";

        // Order matters: more specific patterns first.
        if (Regex.IsMatch(digits, "^4"))                                        return "Visa";
        if (Regex.IsMatch(digits, "^3[47]"))                                    return "Amex";
        if (Regex.IsMatch(digits, "^(5[1-5]|2[2-7])"))                          return "Mastercard";
        if (Regex.IsMatch(digits, "^6(?:011|5|4[4-9])"))                        return "Discover";
        if (Regex.IsMatch(digits, "^(?:2131|1800|35)"))                         return "JCB";
        if (Regex.IsMatch(digits, "^3(?:0[0-5]|[68])"))                         return "Diners";
        return "Unknown";
    }

    private static string OnlyDigits(string s) =>
        new(s.Where(char.IsDigit).ToArray());
}
