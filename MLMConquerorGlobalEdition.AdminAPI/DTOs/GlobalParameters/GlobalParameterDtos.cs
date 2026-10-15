using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.GlobalParameters;

public record GlobalParameterDto(
    int Id, string Key, string Value, string? Description,
    DateTime CreationDate, string CreatedBy, DateTime? LastUpdateDate, string? LastUpdateBy);

public class CreateGlobalParameterRequest
{
    public string  Key         { get; set; } = string.Empty;
    public string  Value       { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>La clave no se cambia: se lee por nombre desde el código, y renombrarla la "borraría".</summary>
public class UpdateGlobalParameterRequest
{
    public string  Value       { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Qué claves gestiona esta pantalla y cómo se validan sus valores. Las direcciones de depósito se
/// validan con cuidado porque cambiar una desvía el dinero de los miembros a otra cartera.
/// </summary>
/// <remarks>
/// Solo las claves <c>Crypto.*</c>. El resto de parámetros tienen su propia pantalla, que valida su
/// valor (un mínimo de consolidación, un porcentaje…); editarlos aquí como texto libre se saltaría esa
/// validación y dejaría al código leyendo un valor que no entiende.
/// </remarks>
public static class GlobalParameterRules
{
    public const string KeyPattern = @"^[A-Za-z0-9._-]{1,200}$";
    public const int ValueMaxLength = 2000;

    /// <summary>Prefijo de las claves que admite el CRUD genérico.</summary>
    public const string ManagedPrefix = "Crypto.";

    /// <summary>Código de error de una clave que no es <c>Crypto.*</c>.</summary>
    public const string NotManagedErrorCode = "GLOBAL_PARAMETER_NOT_MANAGED_HERE";

    // Bech32 (bc1…), base58 (1…/3…/T…) y hex con 0x quedan dentro de esto; lo que no, se rechaza.
    private static readonly Regex DepositAddress = new(@"^(0x)?[A-Za-z0-9]{20,128}$", RegexOptions.Compiled);

    public static bool IsManagedKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.StartsWith(ManagedPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsDepositAddressKey(string key) =>
        key.StartsWith(CryptoDepositKeys.AddressPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsDepositNetworkKey(string key) =>
        key.StartsWith(CryptoDepositKeys.NetworkPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>¿Cambiar esta clave cambia a dónde o por qué red paga el miembro? Entonces se avisa.</summary>
    public static bool IsDepositKey(string key) => IsDepositAddressKey(key) || IsDepositNetworkKey(key);

    /// <summary>
    /// La forma canónica de una clave <c>Crypto.*</c>: el prefijo como lo escribe el código y la moneda
    /// en mayúsculas (<c>crypto.depositaddress.btc</c> → <c>Crypto.DepositAddress.BTC</c>). Los lectores
    /// (CryptoDepositAddressReader) buscan la forma canónica; guardar otra crearía una dirección que nadie
    /// lee junto a la que sí se lee.
    /// </summary>
    public static string Canonicalize(string key)
    {
        key = key.Trim();
        if (IsDepositAddressKey(key)) return CryptoDepositKeys.AddressKey(key[CryptoDepositKeys.AddressPrefix.Length..]);
        if (IsDepositNetworkKey(key)) return CryptoDepositKeys.NetworkKey(key[CryptoDepositKeys.NetworkPrefix.Length..]);
        if (IsManagedKey(key))        return ManagedPrefix + key[ManagedPrefix.Length..];
        return key;
    }

    /// <summary>La moneda de una clave de depósito (dirección o red), o null si no lo es.</summary>
    public static string? CurrencyOf(string key) =>
        IsDepositAddressKey(key) ? key[CryptoDepositKeys.AddressPrefix.Length..].ToUpperInvariant()
        : IsDepositNetworkKey(key) ? key[CryptoDepositKeys.NetworkPrefix.Length..].ToUpperInvariant()
        : null;

    public static string? ValueError(string key, string? value)
    {
        if (value is null) return "Value is required.";
        if (value.Length > ValueMaxLength) return $"Value must be at most {ValueMaxLength} characters.";

        var currency = CurrencyOf(key);
        if (currency is not null && !TokenPurchaseRules.CryptoCurrencies.Contains(currency))
            return $"Unsupported currency '{currency}'. Use one of: {string.Join(", ", TokenPurchaseRules.CryptoCurrencies)}.";

        // Vacío está permitido: es la forma de dejar de ofrecer esa moneda.
        if (IsDepositAddressKey(key) && value.Trim().Length > 0 && !DepositAddress.IsMatch(value.Trim()))
            return "The value is not a valid deposit address.";

        if (IsDepositNetworkKey(key) && value.Trim().Length > 50)
            return "The network note must be at most 50 characters.";

        return null;
    }
}
