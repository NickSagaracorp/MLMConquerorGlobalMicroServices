using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account.StepUp;

public sealed record StepUpChallenge(string ChallengeToken, string Channel, string MaskedTarget, DateTime ExpiresAt);

/// <param name="Message">Frase del servidor; solo sirve para extraer datos (p. ej. la fecha de la espera), nunca se muestra tal cual.</param>
public sealed record StepUpOutcome<T>(bool Success, string? ErrorCode, T? Data, string? Message = null);

/// <summary>
/// Lo que el diálogo necesita del cliente. Interfaz para poder probar el diálogo sin red y para
/// que cada anfitrión decida cómo se autentica la llamada.
/// </summary>
public interface IStepUpClient
{
    Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string operationKey, CancellationToken ct = default);

    Task<StepUpOutcome<string>> VerifyAsync(
        string operationKey, string challengeToken, string code, CancellationToken ct = default);
}

/// <summary>
/// Habla con <c>api/v1/auth/step-up/*</c> de SignupAPI. El cliente con nombre lo registra cada
/// anfitrión con su manejador de autenticación (ver <c>StepUpClientRegistration</c>): este
/// componente no sabe de dónde sale el token de acceso.
/// </summary>
public sealed partial class StepUpClient : IStepUpClient
{
    public const string HttpClientName = "StepUpApi";

    private readonly IHttpClientFactory _factory;

    public StepUpClient(IHttpClientFactory factory) => _factory = factory;

    private sealed class Wrapper<T>
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public T? Data { get; set; }
    }

    private sealed class GrantDto { public string StepUpToken { get; set; } = string.Empty; }

    public Task<StepUpOutcome<StepUpChallenge>> RequestChallengeAsync(string operationKey, CancellationToken ct = default) =>
        PostAsync<StepUpChallenge>("api/v1/auth/step-up/challenge", new { operationKey }, ct);

    public async Task<StepUpOutcome<string>> VerifyAsync(
        string operationKey, string challengeToken, string code, CancellationToken ct = default)
    {
        var r = await PostAsync<GrantDto>("api/v1/auth/step-up/verify",
            new { operationKey, challengeToken, code }, ct);
        return new StepUpOutcome<string>(r.Success, r.ErrorCode, r.Data?.StepUpToken, r.Message);
    }

    private async Task<StepUpOutcome<T>> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        try
        {
            using var resp = await _factory.CreateClient(HttpClientName).PostAsJsonAsync(path, body, ct);
            var env  = await resp.Content.ReadFromJsonAsync<Wrapper<T>>(cancellationToken: ct);
            return env is { Success: true }
                ? new StepUpOutcome<T>(true, null, env.Data)
                : new StepUpOutcome<T>(false, env?.ErrorCode, default, env?.Errors?.FirstOrDefault() ?? env?.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new StepUpOutcome<T>(false, null, default);
        }
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")]
    private static partial Regex IsoMoment();

    /// <summary>
    /// La fecha en que termina la espera, tomada del mensaje del servidor (ISO sin zona, hora de
    /// servidor). Null si el mensaje no la trae: la UI cae entonces al texto genérico.
    /// </summary>
    public static DateTime? TryParseCooldownUntil(string? message)
    {
        if (string.IsNullOrEmpty(message)) return null;
        var m = IsoMoment().Match(message);
        return m.Success && DateTime.TryParseExact(m.Value, "yyyy-MM-ddTHH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
