using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Signups.Client.Payments;

/// <summary>
/// Los métodos de pago del alta que dependen de la configuración del servidor
/// (<c>GET api/v1/signups/payment-options</c>): los países de Entrax y si la tarjeta está encendida.
/// Una lectura para Signup.razor y MemberJoin.razor.
/// </summary>
public static class SignupPaymentOptionsApi
{
    /// <summary>
    /// Lo que dice el servidor. Si no se puede leer, o no es un éxito, FALLA CERRADA: ni tarjeta ni
    /// Entrax. La pantalla solo esconde; quien rechaza de verdad es SignupAPI.
    /// </summary>
    public static async Task<SignupPaymentOptionsDto> GetAsync(HttpClient api, CancellationToken ct = default)
    {
        try
        {
            var r = await api.GetFromJsonAsync<Wrapper>("api/v1/signups/payment-options", ct);
            if (r is { Success: true, Data: not null })
                return new SignupPaymentOptionsDto
                {
                    EntraxCountries = r.Data.EntraxCountries ?? [],
                    CardEnabled     = r.Data.CardEnabled
                };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Caído, 4xx/5xx, un 429 del limitador o algo que no es JSON: lo mismo que apagado.
        }

        return new SignupPaymentOptionsDto();
    }

    private sealed record Wrapper(bool Success, SignupPaymentOptionsDto? Data);
}

/// <summary>
/// Qué pestaña de pago queda elegida en el alta. La de tarjeta solo si el servidor la ofrece: si no, la
/// de por defecto es el token, que es la que cobra (o canjea) de verdad sin intervención.
/// </summary>
public static class SignupPaymentTabs
{
    public const string Card   = "card";
    public const string Token  = "token";
    public const string Crypto = "crypto";
    public const string Entrax = "entrax";

    /// <summary>Antes de leer las opciones del servidor: nunca la tarjeta.</summary>
    public const string Initial = Token;

    public const string CardDisabledError   = "Card payments are not available right now. Please choose another payment method.";
    public const string EntraxDisabledError = "Payment in rubles (SBP) is not available for your country.";
    public const string UnknownTabError     = "Please choose a payment method.";

    public static string Default(bool cardEnabled) => cardEnabled ? Card : Token;

    /// <summary>
    /// Por qué no se puede pagar con esta pestaña, o null si se puede. Se mira ANTES de crear el alta: con la
    /// tarjeta apagada no se crea un alta que luego no se puede pagar así.
    /// </summary>
    public static string? Problem(string? tab, bool cardEnabled, bool entraxOffered) => tab switch
    {
        Token or Crypto => null,
        Card            => cardEnabled ? null : CardDisabledError,
        Entrax          => entraxOffered ? null : EntraxDisabledError,
        _               => UnknownTabError
    };

    /// <summary>
    /// La pestaña elegida si se sigue ofreciendo; si no (tarjeta apagada, Entrax fuera del país, o algo
    /// desconocido), la de por defecto.
    /// </summary>
    public static string Normalize(string? tab, bool cardEnabled, bool entraxOffered) => tab switch
    {
        Card   when cardEnabled   => Card,
        Entrax when entraxOffered => Entrax,
        Token                     => Token,
        Crypto                    => Crypto,
        _                         => Default(cardEnabled)
    };
}
