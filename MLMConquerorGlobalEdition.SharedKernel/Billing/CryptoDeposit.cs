namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Claves de <c>GlobalParameter</c> con las direcciones de depósito cripto. Se administran en
/// Admin → Settings → Global Parameters. Una moneda sin dirección (o con dirección vacía) no se
/// ofrece ni en la compra de tokens ni en el alta.
/// </summary>
public static class CryptoDepositKeys
{
    public const string AddressPrefix = "Crypto.DepositAddress.";
    public const string NetworkPrefix = "Crypto.DepositNetwork.";

    public static string AddressKey(string currency) => AddressPrefix + currency.ToUpperInvariant();
    public static string NetworkKey(string currency) => NetworkPrefix + currency.ToUpperInvariant();
}

public sealed class CryptoDepositAddressDto
{
    public string  Currency { get; set; } = string.Empty;
    public string  Address  { get; set; } = string.Empty;

    /// <summary>Red o nota para el que transfiere (p. ej. "TRC-20"). Opcional.</summary>
    public string? Network  { get; set; }
}
