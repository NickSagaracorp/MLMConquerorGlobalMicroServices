using System.Net.Http.Json;
using System.Text.Json;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Signups.Client.Payments;

/// <summary>Cómo terminó el intento de emitir el QR del alta.</summary>
public enum EntraxStartOutcome
{
    /// <summary>El servidor emitió el QR: <see cref="EntraxStartResult.Payment"/> lo trae.</summary>
    Started,

    /// <summary>El alta ya está pagada y activa (409 SIGNUP_ALREADY_SETTLED): no hay nada que reintentar.</summary>
    AlreadySettled,

    /// <summary>Otra petición seguía creando el QR incluso después de esperar (409 ENTRAX_IN_PROGRESS).</summary>
    InProgress,

    /// <summary>Otra ventana ya tiene un QR vivo de este alta (409 ENTRAX_QR_ALREADY_ACTIVE): no se reintenta.</summary>
    QrAlreadyActive,

    /// <summary>Cualquier otro fallo: <see cref="EntraxStartResult.Error"/> lleva el texto.</summary>
    Failed
}

/// <summary>Resultado de <see cref="EntraxSignupApi.StartAsync"/>.</summary>
public sealed record EntraxStartResult(EntraxStartOutcome Outcome, EntraxSignupPaymentDto? Payment, string? Error);

/// <summary>
/// Las llamadas del alta con Entrax (rublos por SBP) que comparten Signup.razor y MemberJoin.razor,
/// escritas una vez. Corren en el navegador (WebAssembly) contra SignupAPI.
/// </summary>
public static class EntraxSignupApi
{
    // Códigos de error de SignupAPI que la pantalla distingue. SignupAPI no se puede referenciar desde
    // el navegador; una prueba (EntraxSignupApiTests) vigila que no se separen de los del servidor.
    public const string InProgressCode      = "ENTRAX_IN_PROGRESS";
    public const string AlreadySettledCode  = "SIGNUP_ALREADY_SETTLED";
    public const string QrAlreadyActiveCode = "ENTRAX_QR_ALREADY_ACTIVE";

    /// <summary>Veces que se intenta emitir el QR cuando el servidor dice que otra petición lo está creando.</summary>
    public const int InProgressAttempts = 3;

    /// <summary>Espera entre esos intentos.</summary>
    public static readonly TimeSpan InProgressDelay = TimeSpan.FromSeconds(2);

    private const string GenericError    = "Could not create the SBP payment.";
    private const string QrAlreadyActiveError = "There is already an active QR for this signup in another window.";
    private const string InProgressError = "Your SBP payment is still being prepared. Please try again in a moment.";

    /// <summary>Países donde se ofrece. Si no se puede leer, ninguno: la pestaña no aparece.</summary>
    public static async Task<IReadOnlyList<string>> GetCountriesAsync(HttpClient api, CancellationToken ct = default)
    {
        try
        {
            var r = await api.GetFromJsonAsync<Wrapper<SignupPaymentOptionsDto>>("api/v1/signups/payment-options", ct);
            return r?.Data?.EntraxCountries ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Completa el alta con Entrax. Un 409 ENTRAX_IN_PROGRESS (otra petición está creando el QR) se
    /// espera y se reintenta hasta <paramref name="attempts"/> veces; un 409 SIGNUP_ALREADY_SETTLED
    /// termina enseguida y sin reintento (el alta ya está pagada).
    /// </summary>
    public static async Task<EntraxStartResult> StartAsync(
        HttpClient api, string signupId, CancellationToken ct = default,
        int attempts = InProgressAttempts, TimeSpan? retryDelay = null)
    {
        var wait = retryDelay ?? InProgressDelay;
        EntraxStartResult last = new(EntraxStartOutcome.Failed, null, GenericError);

        for (var attempt = 1; attempt <= Math.Max(1, attempts); attempt++)
        {
            last = await StartOnceAsync(api, signupId, ct);
            if (last.Outcome != EntraxStartOutcome.InProgress)
                return last;

            if (attempt < attempts)
                await Task.Delay(wait, ct);
        }

        return last;
    }

    private static async Task<EntraxStartResult> StartOnceAsync(HttpClient api, string signupId, CancellationToken ct)
    {
        using var r = await api.PostAsJsonAsync(
            $"api/v1/signups/{Uri.EscapeDataString(signupId)}/complete",
            new { PaymentMethod = "Entrax" }, ct);

        Wrapper<StartData>? b;
        try
        {
            b = await r.Content.ReadFromJsonAsync<Wrapper<StartData>>(ct);
        }
        catch (JsonException)
        {
            // Un 429 del limitador o un 502 de un proxy no vienen como ApiResponse.
            b = null;
        }

        if (b?.Success == true && b.Data?.Entrax is not null)
            return new(EntraxStartOutcome.Started, b.Data.Entrax, null);

        var error = b?.Message ?? b?.Errors?.FirstOrDefault() ?? GenericError;
        return b?.ErrorCode switch
        {
            AlreadySettledCode => new(EntraxStartOutcome.AlreadySettled, null, error),
            InProgressCode     => new(EntraxStartOutcome.InProgress, null, InProgressError),
            QrAlreadyActiveCode => new(EntraxStartOutcome.QrAlreadyActive, null, QrAlreadyActiveError),
            _                  => new(EntraxStartOutcome.Failed, null, error)
        };
    }

    /// <summary>Estado del pago, o null si el servidor no lo reconoce o no contesta bien.</summary>
    public static async Task<EntraxSignupStatusDto?> GetStatusAsync(
        HttpClient api, string signupId, string paymentRef, CancellationToken ct)
    {
        using var r = await api.GetAsync(
            $"api/v1/signups/{Uri.EscapeDataString(signupId)}/entrax-status?ref={Uri.EscapeDataString(paymentRef)}", ct);
        if (!r.IsSuccessStatusCode) return null;

        var b = await r.Content.ReadFromJsonAsync<Wrapper<EntraxSignupStatusDto>>(ct);
        return b?.Data;
    }

    private sealed record Wrapper<T>(bool Success, T? Data, string? Message, List<string>? Errors, string? ErrorCode);

    private sealed record StartData(EntraxSignupPaymentDto? Entrax);
}
