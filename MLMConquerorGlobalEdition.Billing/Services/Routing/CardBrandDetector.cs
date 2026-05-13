using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

/// <summary>
/// BIN-range based card brand detection.
/// Covers the most common IIN/BIN ranges. Extend as needed.
/// </summary>
public class CardBrandDetector : ICardBrandDetector
{
    public CardBrand Detect(string binOrFirst6)
    {
        if (string.IsNullOrWhiteSpace(binOrFirst6)) return CardBrand.Other;

        var bin = binOrFirst6.Trim().PadLeft(6, '0');
        if (bin.Length < 6) return CardBrand.Other;

        var first6 = bin[..6];
        if (!long.TryParse(first6, out var num)) return CardBrand.Other;

        // Amex: 34xxxx, 37xxxx
        if (first6.StartsWith("34") || first6.StartsWith("37")) return CardBrand.Amex;

        // JCB: 3528–3589
        if (num is >= 352800 and <= 358999) return CardBrand.Jcb;

        // Mastercard: 51–55, 2221–2720
        if (num is >= 510000 and <= 559999) return CardBrand.MasterCard;
        if (num is >= 222100 and <= 272099) return CardBrand.MasterCard;

        // Maestro: 6304, 6759, 676770, 676774
        if (first6.StartsWith("6304") || first6.StartsWith("6759")) return CardBrand.Maestro;
        if (first6.StartsWith("676770") || first6.StartsWith("676774")) return CardBrand.Maestro;

        // Bancontact: 6703xx (Belgium-specific Maestro variant, treated as Bancontact)
        if (first6.StartsWith("6703")) return CardBrand.Bancontact;

        // Visa: starts with 4
        if (first6.StartsWith("4")) return CardBrand.Visa;

        return CardBrand.Other;
    }
}
