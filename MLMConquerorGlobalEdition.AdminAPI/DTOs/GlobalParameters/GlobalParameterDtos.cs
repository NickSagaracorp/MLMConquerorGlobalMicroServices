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
/// Reglas de valor que dependen de la clave. Las direcciones de depósito se validan con cuidado
/// porque cambiar una desvía el dinero de los miembros a otra cartera.
/// </summary>
public static class GlobalParameterRules
{
    public const string KeyPattern = @"^[A-Za-z0-9._-]{1,200}$";
    public const int ValueMaxLength = 2000;

    // Bech32 (bc1…), base58 (1…/3…/T…) y hex con 0x quedan dentro de esto; lo que no, se rechaza.
    private static readonly Regex DepositAddress = new(@"^(0x)?[A-Za-z0-9]{20,128}$", RegexOptions.Compiled);

    public static bool IsDepositAddressKey(string key) =>
        key.StartsWith(CryptoDepositKeys.AddressPrefix, StringComparison.Ordinal);

    public static string? ValueError(string key, string? value)
    {
        if (value is null) return "Value is required.";
        if (value.Length > ValueMaxLength) return $"Value must be at most {ValueMaxLength} characters.";

        if (IsDepositAddressKey(key))
        {
            var currency = key[CryptoDepositKeys.AddressPrefix.Length..];
            if (!TokenPurchaseRules.CryptoCurrencies.Contains(currency))
                return $"Unsupported currency '{currency}'. Use one of: {string.Join(", ", TokenPurchaseRules.CryptoCurrencies)}.";

            // Vacío está permitido: es la forma de dejar de ofrecer esa moneda.
            if (value.Trim().Length > 0 && !DepositAddress.IsMatch(value.Trim()))
                return "The value is not a valid deposit address.";
        }

        if (key.StartsWith(CryptoDepositKeys.NetworkPrefix, StringComparison.Ordinal) && value.Trim().Length > 50)
            return "The network note must be at most 50 characters.";

        return null;
    }
}
