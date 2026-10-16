using Microsoft.Extensions.Configuration;

namespace MLMConquerorGlobalEdition.Repository.Services.Entrax;

/// <summary>
/// Configuración de Entrax (sección "Entrax"). KeyId y Secret NUNCA van en el appsettings.json base:
/// variables de entorno (Entrax__KeyId, Entrax__Secret) o el almacén de secretos del entorno.
/// </summary>
public sealed class EntraxOptions
{
    public const string Section        = "Entrax";
    public const string DefaultBaseUrl = "https://entrax.pro/api/v1/cash-register";

    public static readonly IReadOnlyList<string> DefaultCountries = ["RU"];

    public bool    Enabled     { get; set; }
    public string  BaseUrl     { get; set; } = DefaultBaseUrl;
    public string? KeyId       { get; set; }
    public string? Secret      { get; set; }

    /// <summary>URL pública https de POST api/v1/signups/entrax/callback.</summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Países ISO2 en los que se ofrece. Null o vacío = <see cref="DefaultCountries"/>. Es null por
    /// defecto a propósito: el binder de configuración AÑADE a un array ya inicializado.
    /// </summary>
    public string[]? Countries { get; set; }

    /// <summary>Solo Development: registra FakeEntraxClient en lugar del cliente real.</summary>
    public bool UseFake { get; set; }

    /// <summary>Antigüedad máxima del timestamp de un callback. Nunca menos de 40 (Entrax reintenta 35 min).</summary>
    public int CallbackToleranceMinutes { get; set; } = 60;

    /// <summary>Durante cuánto tiempo tras caducar se sigue mirando si un QR se pagó tarde.</summary>
    public int LatePaymentWindowMinutes { get; set; } = 120;

    /// <summary>Segundos mínimos entre dos consultas a Entrax del mismo pago desde la pantalla.</summary>
    public int PollThrottleSeconds { get; set; } = 15;

    /// <summary>Pagos que consulta el barrido en cada pasada (límite de Entrax: 100/min por IP).</summary>
    public int SweepBatchSize { get; set; } = 50;

    public TimeSpan CallbackTolerance => TimeSpan.FromMinutes(Math.Max(40, CallbackToleranceMinutes));
    public TimeSpan PollThrottle      => TimeSpan.FromSeconds(Math.Max(5, PollThrottleSeconds));
    public TimeSpan LatePaymentWindow => TimeSpan.FromMinutes(Math.Max(0, LatePaymentWindowMinutes));

    public IReadOnlyList<string> EffectiveCountries =>
        (Countries is { Length: > 0 } ? Countries : DefaultCountries)
            .Select(c => (c ?? string.Empty).Trim().ToUpperInvariant())
            .Where(c => c.Length == 2)
            .Distinct()
            .ToArray();

    public bool IsOfferedFor(string? countryIso2)
        => Enabled
        && !string.IsNullOrWhiteSpace(countryIso2)
        && EffectiveCountries.Contains(countryIso2.Trim().ToUpperInvariant());

    public static EntraxOptions FromConfiguration(IConfiguration configuration)
        => configuration.GetSection(Section).Get<EntraxOptions>() ?? new EntraxOptions();
}
