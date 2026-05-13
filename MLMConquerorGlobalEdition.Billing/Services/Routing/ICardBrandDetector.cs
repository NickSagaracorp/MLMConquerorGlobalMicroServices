using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.Routing;

public interface ICardBrandDetector
{
    /// <summary>
    /// Detects the card brand from the first 6 digits of the PAN (BIN).
    /// Returns <see cref="CardBrand.Other"/> when the BIN is unknown.
    /// </summary>
    CardBrand Detect(string binOrFirst6);
}
