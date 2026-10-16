using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.Crypto;

/// <summary>
/// Las direcciones de depósito configuradas en GlobalParameters. Lo usan Billing (compra de tokens)
/// y SignupAPI (alta): una sola lectura para que los dos ofrezcan las mismas monedas.
/// </summary>
public interface ICryptoDepositAddressReader
{
    /// <summary>Monedas candidatas con dirección no vacía, en el orden de TokenPurchaseRules.CryptoCurrencies.</summary>
    Task<IReadOnlyList<CryptoDepositAddressDto>> GetConfiguredAsync(CancellationToken ct = default);
}

public sealed class CryptoDepositAddressReader : ICryptoDepositAddressReader
{
    private readonly AppDbContext _db;

    public CryptoDepositAddressReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CryptoDepositAddressDto>> GetConfiguredAsync(CancellationToken ct = default)
    {
        var keys = TokenPurchaseRules.CryptoCurrencies
            .SelectMany(c => new[] { CryptoDepositKeys.AddressKey(c), CryptoDepositKeys.NetworkKey(c) })
            .ToList();

        var values = await _db.GlobalParameters.AsNoTracking()
            .Where(p => keys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, p => p.Value, ct);

        var result = new List<CryptoDepositAddressDto>();
        foreach (var currency in TokenPurchaseRules.CryptoCurrencies)
        {
            if (!values.TryGetValue(CryptoDepositKeys.AddressKey(currency), out var address)
                || string.IsNullOrWhiteSpace(address))
                continue;

            values.TryGetValue(CryptoDepositKeys.NetworkKey(currency), out var network);
            result.Add(new CryptoDepositAddressDto
            {
                Currency = currency,
                Address  = address.Trim(),
                Network  = string.IsNullOrWhiteSpace(network) ? null : network.Trim()
            });
        }
        return result;
    }
}
