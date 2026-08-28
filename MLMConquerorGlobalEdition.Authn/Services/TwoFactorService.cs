using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <inheritdoc cref="ITwoFactorService"/>
public sealed class TwoFactorService : ITwoFactorService
{
    private const string ChannelUnavailable = "CHANNEL_UNAVAILABLE";
    private const string TooManyAttempts    = "TOO_MANY_ATTEMPTS";
    private const string TooManyRequests    = "TOO_MANY_REQUESTS";
    private const string CodeInvalid        = "CODE_INVALID";
    private const string InvalidChallenge   = "INVALID_CHALLENGE";

    private const string DefaultLanguage = "en";
    private const string Marker          = "1";

    private readonly IChallengeTokenService       _challenges;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService                _email;
    private readonly ISmsService                  _sms;
    private readonly IEncryptionService           _encryption;
    private readonly ICacheService                _cache;
    private readonly IDateTimeProvider            _dateTime;

    private readonly int      _maxAttemptsPerChallenge;
    private readonly int      _maxIssuesPerWindow;
    private readonly TimeSpan _issueWindow;
    private readonly TimeSpan _totpReplayWindow;

    public TwoFactorService(
        IChallengeTokenService       challenges,
        UserManager<ApplicationUser> userManager,
        IEmailService                email,
        ISmsService                  sms,
        IEncryptionService           encryption,
        ICacheService                cache,
        IDateTimeProvider            dateTime,
        IConfiguration               config)
    {
        _challenges  = challenges;
        _userManager = userManager;
        _email       = email;
        _sms         = sms;
        _encryption  = encryption;
        _cache       = cache;
        _dateTime    = dateTime;

        _maxAttemptsPerChallenge = ReadPositiveInt(config, "Auth:TwoFactor:MaxAttemptsPerChallenge", 5);
        _maxIssuesPerWindow      = ReadPositiveInt(config, "Auth:TwoFactor:MaxIssuesPerWindow", 3);
        _issueWindow             = TimeSpan.FromMinutes(
            ReadPositiveInt(config, "Auth:TwoFactor:IssueWindowMinutes", 15));
        _totpReplayWindow        = TimeSpan.FromSeconds(
            ReadPositiveInt(config, "Auth:TwoFactor:TotpReplayWindowSeconds", 90));
    }

    // ── emisión ──────────────────────────────────────────────────────────────

    public async Task<Result<ChallengeIssued>> IssueAsync(
        ApplicationUser   user,
        TwoFactorPurpose  purpose,
        string?           operationKey = null,
        TwoFactorChannel? forcedChannel = null,
        string?           languageCode = null,
        CancellationToken ct = default)
    {
        var channel = forcedChannel ?? user.PreferredTwoFactorChannel;

        // Comprobar disponibilidad antes que nada: pedir un SMS a quien no ha confirmado su
        // teléfono es un error de configuración, no un intento de abuso, y no debe gastar
        // cupo de emisiones ni generar un código que no va a ninguna parte.
        var target = ResolveTarget(user, channel);
        if (target is null)
            return Result<ChallengeIssued>.Failure(
                ChannelUnavailable, $"El canal {channel} no está disponible para este usuario.");

        var window = await ReadIssueWindowAsync(user.Id, ct);
        if (window.Count >= _maxIssuesPerWindow)
            return Result<ChallengeIssued>.Failure(
                TooManyRequests,
                "Se han pedido demasiados códigos; espere unos minutos antes de volver a intentarlo.");

        var language = string.IsNullOrWhiteSpace(languageCode) ? DefaultLanguage : languageCode;

        // Authenticator no lleva código nuestro: lo genera la aplicación del usuario y lo
        // verifica Identity, así que el challenge va sin hash contra el que comparar.
        var code     = channel == TwoFactorChannel.Authenticator ? null : _challenges.GenerateCode();
        var codeHash = code is null ? null : _challenges.HashCode(code);

        try
        {
            await DispatchAsync(channel, target, code, language, ct);
        }
        catch (Exception)
        {
            // Sin despacho no hay challenge. Devolver uno dejaría al usuario esperando un
            // código que nunca va a llegar, sin manera de saber que el problema es el
            // transporte; con CHANNEL_UNAVAILABLE la interfaz puede ofrecerle otro canal.
            return Result<ChallengeIssued>.Failure(
                ChannelUnavailable, $"No se pudo entregar el código por {channel}.");
        }

        var challengeToken = _challenges.Issue(
            user.Id, user.Email!, purpose, channel, codeHash, operationKey);

        await SaveIssueWindowAsync(user.Id, window, ct);

        return Result<ChallengeIssued>.Success(new ChallengeIssued(
            ChallengeToken: challengeToken,
            Channel:        channel,
            MaskedTarget:   MaskTarget(channel, target),
            ExpiresAt:      _dateTime.Now.Add(_challenges.ChallengeLifetime)));
    }

    /// <summary>
    /// A dónde se manda el código por este canal, o null si el canal no está disponible.
    /// Para SMS devuelve el teléfono ya descifrado: se guarda cifrado porque es a la vez PII
    /// y factor de autenticación.
    /// </summary>
    private string? ResolveTarget(ApplicationUser user, TwoFactorChannel channel) => channel switch
    {
        TwoFactorChannel.Email =>
            string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,

        TwoFactorChannel.Sms =>
            user.TwoFactorPhoneConfirmed && !string.IsNullOrWhiteSpace(user.TwoFactorPhoneEncrypted)
                ? _encryption.Decrypt(user.TwoFactorPhoneEncrypted)
                : null,

        // Sin enrolamiento confirmado no hay clave que Identity pueda verificar: emitir el
        // challenge dejaría al usuario ante una pantalla de código que nunca va a aceptar nada.
        TwoFactorChannel.Authenticator =>
            user.TwoFactorEnrolledAt is null ? null : string.Empty,

        _ => null
    };

    private async Task DispatchAsync(
        TwoFactorChannel channel, string target, string? code, string language, CancellationToken ct)
    {
        if (channel == TwoFactorChannel.Authenticator)
            return;

        var variables = new Dictionary<string, string>
        {
            ["Code"]             = code!,
            ["ExpiresInMinutes"] = ((int)_challenges.ChallengeLifetime.TotalMinutes).ToString()
        };

        if (channel == TwoFactorChannel.Email)
        {
            await _email.SendAsync(
                toEmail:      target,
                toName:       target,
                languageCode: language,
                eventType:    NotificationEvents.TwoFactorCode,
                variables:    variables,
                ct:           ct);
            return;
        }

        await _sms.SendAsync(
            toPhoneE164:  target,
            languageCode: language,
            eventType:    NotificationEvents.TwoFactorCode,
            variables:    variables,
            ct:           ct);
    }

    // ── verificación ─────────────────────────────────────────────────────────

    public async Task<Result<ChallengeClaims>> VerifyAsync(
        string            challengeToken,
        string            code,
        TwoFactorPurpose  expectedPurpose,
        string?           expectedOperationKey = null,
        CancellationToken ct = default)
    {
        var validation = _challenges.Validate(challengeToken, expectedPurpose, expectedOperationKey);
        if (!validation.IsSuccess)
            return validation;

        var claims = validation.Value!;

        // Los intentos se miran antes que la marca de consumido: al agotarlos el challenge
        // se quema, y quien insista tiene que enterarse de que el motivo es el límite y no
        // un token cualquiera inválido. Además así el sexto intento falla aunque el código
        // sea el correcto, que es justo lo que impide probar indefinidamente.
        var attempts = await ReadAttemptsAsync(claims.Jti, ct);
        if (attempts >= _maxAttemptsPerChallenge)
        {
            await BurnAsync(claims, ct);
            return Result<ChallengeClaims>.Failure(
                TooManyAttempts, "Demasiados intentos fallidos; solicite un código nuevo.");
        }

        if (await IsConsumedAsync(claims.Jti, ct))
            return Result<ChallengeClaims>.Failure(
                InvalidChallenge, "Este challenge ya se usó; solicite un código nuevo.");

        var codeHash = _challenges.HashCode(code);

        var verified = claims.Channel == TwoFactorChannel.Authenticator
            ? await VerifyAuthenticatorAsync(claims, code, codeHash, ct)
            : VerifyStoredHash(claims.CodeHash, codeHash);

        if (!verified)
        {
            await RegisterFailedAttemptAsync(claims, attempts, ct);
            return Result<ChallengeClaims>.Failure(CodeInvalid, "El código introducido no es válido.");
        }

        // Antirreplay del challenge: la firma sigue siendo válida hasta su exp, así que sin
        // esta marca el mismo token se redimiría dos veces dentro de su ventana de vida.
        await MarkConsumedAsync(claims, ct);

        if (claims.Channel == TwoFactorChannel.Authenticator)
            await _cache.SetAsync(
                CacheKeys.TwoFactorTotpUsed(claims.UserId, codeHash), Marker, _totpReplayWindow, ct);

        return Result<ChallengeClaims>.Success(claims);
    }

    private async Task<bool> VerifyAuthenticatorAsync(
        ChallengeClaims claims, string code, string codeHash, CancellationToken ct)
    {
        // Antirreplay del código TOTP: Identity lo sigue aceptando durante la tolerancia de
        // reloj (~90 s). En una operación de dinero eso serían dos autorizaciones con el
        // mismo código, cada una con su challenge distinto — el jti no las relaciona.
        if (await _cache.GetAsync<string>(CacheKeys.TwoFactorTotpUsed(claims.UserId, codeHash), ct) is not null)
            return false;

        var user = await _userManager.FindByIdAsync(claims.UserId);
        if (user is null)
            return false;

        return await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, code);
    }

    /// <summary>
    /// Compara el hash del código recibido con el que viaja en el challenge en tiempo
    /// constante. Con <c>==</c> sobre cadenas el tiempo de respuesta filtraría cuántos
    /// caracteres iniciales coinciden, y eso convierte una búsqueda de un millón de
    /// combinaciones en seis búsquedas de diez.
    /// </summary>
    private static bool VerifyStoredHash(string? expectedHash, string actualHash)
    {
        if (string.IsNullOrEmpty(expectedHash))
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(expectedHash),
                Convert.FromBase64String(actualHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // ── límites y antirreplay ────────────────────────────────────────────────

    private async Task RegisterFailedAttemptAsync(
        ChallengeClaims claims, int previousAttempts, CancellationToken ct)
    {
        var attempts = previousAttempts + 1;

        await _cache.SetAsync(
            CacheKeys.TwoFactorAttempts(claims.Jti),
            attempts.ToString(),
            RemainingLifetime(claims),
            ct);

        if (attempts >= _maxAttemptsPerChallenge)
            await BurnAsync(claims, ct);
    }

    /// <summary>Quema el challenge: agotados los intentos, hay que pedir uno nuevo.</summary>
    private Task BurnAsync(ChallengeClaims claims, CancellationToken ct) => MarkConsumedAsync(claims, ct);

    private Task MarkConsumedAsync(ChallengeClaims claims, CancellationToken ct) =>
        _cache.SetAsync(
            CacheKeys.TwoFactorChallengeConsumed(claims.Jti), Marker, RemainingLifetime(claims), ct);

    private async Task<bool> IsConsumedAsync(string jti, CancellationToken ct) =>
        await _cache.GetAsync<string>(CacheKeys.TwoFactorChallengeConsumed(jti), ct) is not null;

    /// <summary>
    /// Intentos fallidos ya registrados para este challenge.
    ///
    /// OJO: <c>ICacheService</c> solo ofrece Get/Set/Remove, así que esto es un
    /// leer-modificar-escribir y no un incremento atómico. Bajo peticiones concurrentes dos
    /// intentos pueden leer el mismo valor y escribir el mismo incremento, de modo que el
    /// límite se estira. Cerrar el hueco de verdad exige un contador atómico en el backend
    /// (Redis INCR + EXPIRE), que hoy la abstracción no expone.
    /// </summary>
    private async Task<int> ReadAttemptsAsync(string jti, CancellationToken ct)
    {
        var raw = await _cache.GetAsync<string>(CacheKeys.TwoFactorAttempts(jti), ct);
        return int.TryParse(raw, out var attempts) ? attempts : 0;
    }

    /// <summary>
    /// Ventana de emisiones vigente del usuario, o una nueva si la anterior ya venció.
    /// Mismo aviso que <see cref="ReadAttemptsAsync"/>: sin incremento atómico, un ráfaga
    /// concurrente puede colar alguna emisión de más.
    /// </summary>
    private async Task<TwoFactorIssueWindow> ReadIssueWindowAsync(string userId, CancellationToken ct)
    {
        var now      = _dateTime.Now;
        var existing = await _cache.GetAsync<TwoFactorIssueWindow>(CacheKeys.TwoFactorIssueWindow(userId), ct);

        return existing is null || now - existing.WindowStart >= _issueWindow
            ? new TwoFactorIssueWindow(0, now)
            : existing;
    }

    /// <summary>
    /// Anota una emisión más. Solo se llama tras un despacho con éxito: lo que se limita es
    /// el gasto y el ruido que llega al usuario, y un transporte caído no produce ninguno de
    /// los dos. El TTL se calcula contra el inicio de la ventana, no contra el momento
    /// actual, para que emitir no la estire.
    /// </summary>
    private Task SaveIssueWindowAsync(string userId, TwoFactorIssueWindow window, CancellationToken ct)
    {
        var elapsed   = _dateTime.Now - window.WindowStart;
        var remaining = _issueWindow - elapsed;
        if (remaining < TimeSpan.FromSeconds(1))
            remaining = TimeSpan.FromSeconds(1);

        return _cache.SetAsync(
            CacheKeys.TwoFactorIssueWindow(userId),
            window with { Count = window.Count + 1 },
            remaining,
            ct);
    }

    /// <summary>
    /// Lo que le queda de vida al challenge. Se mide con UtcNow, no con Now: <c>exp</c> viene
    /// del token y los tiempos de un JWT son epoch UTC por especificación.
    /// </summary>
    private TimeSpan RemainingLifetime(ChallengeClaims claims)
    {
        var remaining = claims.ExpiresAt - _dateTime.UtcNow;
        return remaining < TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : remaining;
    }

    // ── enmascarado ──────────────────────────────────────────────────────────

    private static string MaskTarget(TwoFactorChannel channel, string target) => channel switch
    {
        TwoFactorChannel.Email => MaskEmail(target),
        TwoFactorChannel.Sms   => MaskPhone(target),
        _                      => string.Empty
    };

    /// <summary>
    /// Primera letra, asteriscos y el dominio intacto. Copiada de
    /// <c>SignupAPI/Services/TwoFactorChallengeService</c> para no cambiar lo que ve el
    /// usuario cuando el login pase por aquí.
    /// </summary>
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0) return email;
        var local   = email[..atIndex];
        var domain  = email[(atIndex + 1)..];
        var visible = local.Length <= 1 ? local : local[..1];
        return $"{visible}{new string('*', Math.Max(1, local.Length - 1))}@{domain}";
    }

    /// <summary>Solo los cuatro últimos dígitos: lo justo para que el usuario reconozca su
    /// teléfono sin que la pantalla lo revele a quien la esté mirando.</summary>
    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var trimmed = phone.Trim();
        return trimmed.Length <= 4
            ? new string('*', trimmed.Length)
            : new string('*', trimmed.Length - 4) + trimmed[^4..];
    }

    private static int ReadPositiveInt(IConfiguration config, string key, int fallback) =>
        int.TryParse(config[key], out var value) && value > 0 ? value : fallback;
}
