using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Services;

/// <summary>
/// Avisa por correo a todos los SuperAdmin activos de que cambió una dirección de depósito cripto.
/// </summary>
public interface ICryptoDepositAlertNotifier
{
    /// <param name="key">La clave <c>Crypto.DepositAddress.{MONEDA}</c>.</param>
    /// <param name="oldValue">El valor anterior; null si el parámetro se acaba de crear.</param>
    /// <param name="newValue">El valor guardado (vacío = la moneda deja de ofrecerse).</param>
    /// <param name="actor">Quién lo cambió, como queda en el rastro del parámetro.</param>
    Task NotifyAsync(string key, string? oldValue, string newValue, string actor, CancellationToken ct = default);
}

/// <inheritdoc cref="ICryptoDepositAlertNotifier"/>
/// <remarks>
/// <b>NUNCA LANZA.</b> Se llama con el cambio ya guardado: un fallo de SES no puede convertir en
/// error un cambio que sí ocurrió (la pantalla diría "falló" y alguien lo repetiría). Cada destinatario
/// va por separado para que un buzón que rebota no deje sin aviso a los demás.
///
/// Mientras el panel admin no tenga step-up (riesgo R8 del Plan F), este aviso es el único freno a
/// que una cuenta Admin comprometida desvíe los depósitos sin que nadie se entere: por eso va a
/// todos los SuperAdmin y no solo a quien hizo el cambio.
/// </remarks>
public sealed class CryptoDepositAlertNotifier : ICryptoDepositAlertNotifier
{
    private readonly AppDbContext      _db;
    private readonly IEmailService     _email;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CryptoDepositAlertNotifier> _logger;

    public CryptoDepositAlertNotifier(
        AppDbContext db, IEmailService email, IDateTimeProvider clock,
        ILogger<CryptoDepositAlertNotifier> logger)
    {
        _db     = db;
        _email  = email;
        _clock  = clock;
        _logger = logger;
    }

    public async Task NotifyAsync(string key, string? oldValue, string newValue, string actor, CancellationToken ct = default)
    {
        var currency = key.StartsWith(CryptoDepositKeys.AddressPrefix, StringComparison.Ordinal)
            ? key[CryptoDepositKeys.AddressPrefix.Length..]
            : key;

        var variables = new Dictionary<string, string>
        {
            ["Currency"]   = currency,
            ["OldAddress"] = oldValue ?? string.Empty,
            ["NewAddress"] = newValue,
            ["ChangedBy"]  = actor,
            // Hora de servidor, la misma que sella el historial.
            ["ChangedAt"]  = _clock.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        };

        List<Repository.Identity.ApplicationUser> recipients;
        try
        {
            // El cambio se guardó con el token ya cancelable de la petición; la búsqueda y los envíos
            // usan CancellationToken.None para que cerrar la pestaña no deje el aviso a medias.
            var superAdminRoleIds = _db.Roles
                .Where(r => r.Name == AppRoles.SuperAdmin)
                .Select(r => r.Id);

            recipients = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Email != null && u.Email != ""
                            && _db.UserRoles.Any(ur => ur.UserId == u.Id && superAdminRoleIds.Contains(ur.RoleId)))
                .OrderBy(u => u.Email)
                .ToListAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo buscar a los SuperAdmin para avisar del cambio de {Key}; el cambio se mantiene.", key);
            return;
        }

        if (recipients.Count == 0)
        {
            _logger.LogWarning("Cambio de {Key} sin SuperAdmin activo a quien avisar.", key);
            return;
        }

        foreach (var user in recipients)
        {
            try
            {
                var language = await TwoFactorLanguage.ResolveAsync(_db, user, CancellationToken.None) ?? "en";

                await _email.SendAsync(
                    toEmail:      user.Email!,
                    toName:       user.Email!,
                    languageCode: language,
                    eventType:    NotificationEvents.CryptoDepositAddressChanged,
                    variables:    variables,
                    ct:           CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo avisar al SuperAdmin {UserId} del cambio de {Key}; el cambio se mantiene.",
                    user.Id, key);
            }
        }
    }
}
