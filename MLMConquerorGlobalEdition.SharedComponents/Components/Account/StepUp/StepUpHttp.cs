using System.Net;
using System.Net.Http.Json;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

/// <summary>
/// Lo mínimo para que un componente reconozca el 403 de step-up y reintente con el permiso.
/// Vive aparte del diálogo para que el ciclo "enviar, pedir código, reintentar" se escriba una vez
/// y lo usen todos los componentes que cambian el cobro, en cualquier portal.
/// </summary>
public static class StepUpHttp
{
    private sealed class Envelope { public string? ErrorCode { get; set; } }

    /// <remarks>
    /// Lee el cuerpo con LoadIntoBufferAsync para que quien llama pueda volver a leerlo si no era
    /// step-up: sin el búfer, el segundo intento de lectura encontraría el flujo consumido.
    /// </remarks>
    private static async Task<string?> ErrorCodeOfForbiddenAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Forbidden) return null;
        try
        {
            await response.Content.LoadIntoBufferAsync();
            var body = await response.Content.ReadFromJsonAsync<Envelope>();
            return body?.ErrorCode;
        }
        catch { return null; }
    }

    /// <summary>¿La API pide el código de verificación (403 STEP_UP_REQUIRED)?</summary>
    public static async Task<bool> IsStepUpRequiredAsync(HttpResponseMessage response) =>
        string.Equals(await ErrorCodeOfForbiddenAsync(response), StepUpGrant.RequiredErrorCode, StringComparison.Ordinal);

    /// <summary>
    /// ¿La API rechazó el reintento por una razón de step-up (STEP_UP_INVALID, STEP_UP_REQUIRED de nuevo o STEP_UP_COOLDOWN)? Pasa si otra
    /// petición lo gastó antes o si el almacén no respondió; la persona no puede arreglarlo más que
    /// repitiendo, y el cuerpo crudo no le dice eso.
    /// </summary>
    public static async Task<bool> IsGrantRejectedAsync(HttpResponseMessage response) =>
        await ErrorCodeOfForbiddenAsync(response) is
            StepUpGrant.RequiredErrorCode or StepUpGrant.InvalidErrorCode or StepUpGrant.CooldownErrorCode;

    public static HttpRequestMessage WithGrant(HttpRequestMessage request, string? stepUpToken)
    {
        if (!string.IsNullOrEmpty(stepUpToken))
            request.Headers.TryAddWithoutValidation(StepUpGrant.HeaderName, stepUpToken);
        return request;
    }

    /// <summary>
    /// Envía la petición; si la API responde STEP_UP_REQUIRED, pide el permiso con
    /// <paramref name="requireGrant"/> y la repite UNA vez con él. Null si la persona canceló.
    /// </summary>
    /// <remarks>
    /// <paramref name="build"/> es una fábrica y no una petición porque un HttpRequestMessage solo
    /// se puede enviar una vez. El reintento es único a propósito: si el permiso recién emitido
    /// tampoco vale, repetir en bucle solo quemaría códigos.
    /// </remarks>
    public static async Task<HttpResponseMessage?> SendAsync(
        HttpClient http, string operationKey, Func<HttpRequestMessage> build, Func<string, Task<string?>> requireGrant)
    {
        var resp = await http.SendAsync(build());
        if (!await IsStepUpRequiredAsync(resp)) return resp;

        // El 403 inicial ya cumplió su función; se libera antes de esperar a la persona.
        resp.Dispose();
        var grant = await requireGrant(operationKey);
        if (grant is null) return null;
        return await http.SendAsync(WithGrant(build(), grant));
    }
}
