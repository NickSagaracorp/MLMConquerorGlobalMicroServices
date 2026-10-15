using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
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

    /// <summary>
    /// El almacén de marcas de un solo uso no respondió. Se cierra en vez de dar por buena la
    /// verificación: sin poder marcar el reto como usado, el mismo código serviría otra vez.
    /// </summary>
    private const string VerificationUnavailable = "VERIFICATION_UNAVAILABLE";

    /// <summary>
    /// Se alcanzó el tope diario de códigos fallidos para este ámbito (login o step-up): no se
    /// verifica ni se emite nada más hasta que venza la ventana.
    /// </summary>
    private const string TwoFactorLocked = "TWO_FACTOR_LOCKED";

    private const string LockedMessage =
        "Demasiados códigos incorrectos; la verificación queda bloqueada hasta 24 horas.";

    /// <summary>Ámbitos del contador diario de fallos. Login y enrolamiento comparten el suyo.</summary>
    internal const string LoginFailureScope  = "login";
    internal const string StepUpFailureScope = "stepup";

    /// <summary>
    /// Ventana del tope diario. Fija desde el primer fallo, no deslizante: el contador atómico de la
    /// caché fija el vencimiento al crearse. En el peor caso caben dos topes seguidos a caballo del
    /// vencimiento, y después nunca más de un tope por ventana.
    /// </summary>
    private static readonly TimeSpan FailureWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Valores de la ventana antirreplay del TOTP de los que ya se avisó al subirlos. El servicio es
    /// Scoped: sin esto, una configuración por debajo del suelo escribiría el aviso en cada petición
    /// de segundo factor. Por valor y no un solo indicador, para que cambiar la configuración a otro
    /// valor también inválido vuelva a avisar.
    /// </summary>
    private static readonly ConcurrentDictionary<int, byte> WarnedTotpReplayValues = new();

    private const string DefaultLanguage = "en";

    private readonly IChallengeTokenService       _challenges;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService                _email;
    private readonly ISmsService                  _sms;
    private readonly IEncryptionService           _encryption;
    private readonly ICacheService                _cache;
    private readonly IDateTimeProvider            _dateTime;
    private readonly ILogger<TwoFactorService>    _logger;
    private readonly ISecurityFactorNotifier?     _notifier;

    private readonly int      _maxAttemptsPerChallenge;
    private readonly int      _maxIssuesPerWindow;
    private readonly TimeSpan _issueWindow;
    private readonly TimeSpan _totpReplayWindow;
    private readonly int      _maxLoginFailuresPerDay;
    private readonly int      _maxStepUpFailuresPerDay;

    public TwoFactorService(
        IChallengeTokenService       challenges,
        UserManager<ApplicationUser> userManager,
        IEmailService                email,
        ISmsService                  sms,
        IEncryptionService           encryption,
        ICacheService                cache,
        IDateTimeProvider            dateTime,
        IConfiguration               config,
        ILogger<TwoFactorService>?   logger = null,
        ISecurityFactorNotifier?     notifier = null)
    {
        _logger      = logger ?? NullLogger<TwoFactorService>.Instance;
        // Opcional: sin él el bloqueo se aplica igual y solo queda constancia en el log. Lo
        // registran los anfitriones que ya avisan de cambios de factor (SignupAPI).
        _notifier    = notifier;
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
        _totpReplayWindow        = TimeSpan.FromSeconds(ResolveTotpReplayWindowSeconds(config, _logger));

        _maxLoginFailuresPerDay  = ReadPositiveInt(config, "Auth:TwoFactor:MaxFailuresPerDay", 15);
        _maxStepUpFailuresPerDay = ReadPositiveInt(config, "Auth:StepUp:MaxFailuresPerDay", 10);
    }

    /// <summary>
    /// La ventana antirreplay del TOTP, nunca por debajo de <see cref="MinTotpReplayWindowSeconds"/>.
    /// Si la configuración pide menos se sube, y se avisa una vez por valor: quien la bajó cree que
    /// tiene otra ventana, y un ajuste que se ignora en silencio no lo descubre nadie.
    /// </summary>
    private static int ResolveTotpReplayWindowSeconds(IConfiguration config, ILogger logger)
    {
        var configured = ReadPositiveInt(config, "Auth:TwoFactor:TotpReplayWindowSeconds", 180);
        if (configured >= MinTotpReplayWindowSeconds)
            return configured;

        if (WarnedTotpReplayValues.TryAdd(configured, 0))
            logger.LogWarning(
                "2FA: Auth:TwoFactor:TotpReplayWindowSeconds = {Configured} está por debajo del mínimo; se usa {Minimum} s. " +
                "Identity acepta un mismo código TOTP unos 150 s y una ventana menor lo dejaría reutilizable.",
                configured, MinTotpReplayWindowSeconds);

        return MinTotpReplayWindowSeconds;
    }

    /// <summary>
    /// Suelo de la ventana antirreplay del TOTP. Identity (AuthenticatorTokenProvider) acepta el
    /// código de los pasos -2 a +2 de 30 s, así que un mismo código sigue valiendo unos 150 s. Si
    /// la marca de "ya usado" caducara antes, el código volvería a servir dentro de su propia
    /// vigencia y el antirreplay sería un adorno. Un valor configurado por debajo se sube aquí, con
    /// un aviso en el log: no es una preferencia que se pueda bajar sin romper la garantía.
    /// </summary>
    internal const int MinTotpReplayWindowSeconds = 150;

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
        var target = ResolveTarget(user, channel, purpose);
        if (target is null)
            return Result<ChallengeIssued>.Failure(
                ChannelUnavailable, $"El canal {channel} no está disponible para este usuario.");

        // El bloqueo diario va antes del cupo: a quien ya agotó sus fallos no se le manda nada, ni
        // se le gasta una emisión que no podría usar. Si el contador no se puede leer se cierra:
        // emitir sin saber si la cuenta está bloqueada es justo lo que el tope tiene que impedir.
        long failures;
        try
        {
            failures = await _cache.GetCounterAsync(FailuresKey(user.Id, purpose), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "2FA: no se pudo leer el contador de fallos del usuario {UserId} (propósito {Purpose}); no se emite.",
                user.Id, purpose);
            return Result<ChallengeIssued>.Failure(
                VerificationUnavailable, "No se pudo completar la verificación; inténtelo de nuevo en unos minutos.");
        }

        if (failures >= MaxFailuresFor(purpose))
        {
            _logger.LogWarning(
                "2FA: emisión rechazada, segundo factor bloqueado para el usuario {UserId} (propósito {Purpose}).",
                user.Id, purpose);
            return Result<ChallengeIssued>.Failure(TwoFactorLocked, LockedMessage);
        }

        // El cupo se reserva antes de emitir, no se anota después. Un contador atómico solo
        // sirve como tope si el incremento y la comprobación son el mismo paso: leer primero y
        // escribir después deja el hueco por el que N peticiones simultáneas leen el mismo
        // valor y pasan todas. El precio es que un despacho fallido gasta cupo, lo que es
        // preferible a que una ráfaga concurrente lo salte entero.
        var quotaKey = IssueWindowKey(user.Id, purpose);
        var issues   = await _cache.IncrementAsync(quotaKey, _issueWindow, ct);

        if (issues > _maxIssuesPerWindow)
            return Result<ChallengeIssued>.Failure(
                TooManyRequests,
                "Se han pedido demasiados códigos; espere unos minutos antes de volver a intentarlo.");

        var language = string.IsNullOrWhiteSpace(languageCode) ? DefaultLanguage : languageCode;

        // Authenticator no lleva código nuestro: lo genera la aplicación del usuario y lo
        // verifica Identity, así que el challenge va sin huella contra la que comparar. La huella
        // de correo y SMS la calcula el propio servicio de retos al emitir, atada al jti.
        var code = channel == TwoFactorChannel.Authenticator ? null : _challenges.GenerateCode();

        try
        {
            await DispatchAsync(channel, target, code, language, purpose, ct);
        }
        catch (Exception)
        {
            // El cupo se apunta antes de despachar —tiene que ser así para que el tope sea
            // real— pero aquí no se envió nada, así que se devuelve. Sin esto, una caída de
            // SES o Twilio consumiría las tres emisiones del usuario y lo dejaría fuera
            // quince minutos, sin poder siquiera probar otro canal: con 2FA obligatorio, la
            // caída de un proveedor externo se convertiría en un bloqueo de acceso.
            //
            // Devolverlo no reabre el hueco que cerró el contador atómico: solo ocurre
            // cuando el mensaje no llegó a salir, y quien ataca no controla que el proveedor
            // se caiga.
            await _cache.DecrementAsync(quotaKey, ct);

            // Sin despacho no hay challenge. Devolver uno dejaría al usuario esperando un
            // código que nunca va a llegar, sin manera de saber que el problema es el
            // transporte; con CHANNEL_UNAVAILABLE la interfaz puede ofrecerle otro canal.
            return Result<ChallengeIssued>.Failure(
                ChannelUnavailable, $"No se pudo entregar el código por {channel}.");
        }

        var challengeToken = _challenges.Issue(
            user.Id, user.Email!, purpose, channel, code, operationKey);

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
    /// <param name="purpose">
    /// Entra en la decisión solo por el SMS sin confirmar, y solo para el enrolamiento: ver el
    /// comentario del caso <see cref="TwoFactorChannel.Sms"/>.
    /// </param>
    private string? ResolveTarget(
        ApplicationUser user, TwoFactorChannel channel, TwoFactorPurpose purpose) => channel switch
    {
        TwoFactorChannel.Email =>
            string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,

        // El teléfono todavía sin confirmar solo vale para el enrolamiento, que es justo el
        // trámite que lo confirma: para verificar un número hay que mandarle un SMS, y exigir
        // que ya estuviera confirmado haría imposible darlo de alta nunca.
        //
        // Para cualquier otro propósito sigue sin existir: un número que nadie ha demostrado
        // tener no es un segundo factor. Si el login lo aceptara, bastaría con dar de alta un
        // teléfono cualquiera para desviar ahí los códigos de acceso sin verificar nada.
        TwoFactorChannel.Sms =>
            string.IsNullOrWhiteSpace(user.TwoFactorPhoneEncrypted)
                ? null
                : user.TwoFactorPhoneConfirmed || purpose == TwoFactorPurpose.Enrollment
                    ? _encryption.Decrypt(user.TwoFactorPhoneEncrypted)
                    : null,

        // Sin enrolamiento confirmado no hay clave que Identity pueda verificar: emitir el
        // challenge dejaría al usuario ante una pantalla de código que nunca va a aceptar nada.
        TwoFactorChannel.Authenticator =>
            user.TwoFactorEnrolledAt is null ? null : string.Empty,

        _ => null
    };

    /// <summary>
    /// El cupo de emisiones en el que cuenta este propósito. El step-up va aparte: pedir step-ups
    /// con una sesión robada no puede dejar al dueño sin códigos para entrar.
    /// </summary>
    private static string FailuresKey(string userId, TwoFactorPurpose purpose) =>
        CacheKeys.TwoFactorFailures(
            userId, purpose == TwoFactorPurpose.StepUp ? StepUpFailureScope : LoginFailureScope);

    private int MaxFailuresFor(TwoFactorPurpose purpose) =>
        purpose == TwoFactorPurpose.StepUp ? _maxStepUpFailuresPerDay : _maxLoginFailuresPerDay;

    private static string IssueWindowKey(string userId, TwoFactorPurpose purpose) =>
        purpose == TwoFactorPurpose.StepUp
            ? CacheKeys.StepUpIssueWindow(userId)
            : CacheKeys.TwoFactorIssueWindow(userId);

    /// <summary>
    /// La plantilla según para qué es el código. El de step-up tiene la suya porque tiene que decir
    /// qué autoriza: un "tu código de verificación" genérico no le avisa a nadie de que alguien está
    /// cambiando a dónde se le paga.
    /// </summary>
    private static string EventFor(TwoFactorPurpose purpose) =>
        purpose == TwoFactorPurpose.StepUp ? NotificationEvents.StepUpCode : NotificationEvents.TwoFactorCode;

    private async Task DispatchAsync(
        TwoFactorChannel channel, string target, string? code, string language,
        TwoFactorPurpose purpose, CancellationToken ct)
    {
        if (channel == TwoFactorChannel.Authenticator)
            return;

        var eventType = EventFor(purpose);

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
                eventType:    eventType,
                variables:    variables,
                ct:           ct);
            return;
        }

        await _sms.SendAsync(
            toPhoneE164:  target,
            languageCode: language,
            eventType:    eventType,
            variables:    variables,
            ct:           ct);
    }

    // ── enrolamiento ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string IssueEnrollmentToken(ApplicationUser user) =>
        // Sin código ni hash: aquí no se despacha nada porque el usuario todavía no tiene
        // dónde recibirlo — es justo lo que va a configurar. El canal es Authenticator
        // precisamente porque es el único que el challenge admite sin hash.
        //
        // No gasta cupo de emisiones ni toca la caché: no hay nada que limitar cuando no sale
        // ningún mensaje, y quien se queda atrapado en el enrolamiento no debe poder quedarse
        // además sin poder reintentarlo.
        _challenges.Issue(
            userId:       user.Id,
            email:        user.Email!,
            purpose:      TwoFactorPurpose.Enrollment,
            channel:      TwoFactorChannel.Authenticator,
            code:         null,
            operationKey: null);

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

        // Tope diario por usuario, encima del de cada reto. Con 5 intentos por reto y 3 retos cada
        // 15 minutos cabían ~1.440 códigos al día contra la misma cuenta; esto lo baja a 15 (10 en
        // step-up, que autoriza dinero), con contadores separados para que quemar el step-up con
        // una sesión robada no deje al dueño sin poder entrar.
        //
        // Se RESERVA al entrar y se devuelve si el intento no resultó ser un código equivocado, por
        // lo mismo que el contador de cada reto: leer el tope y apuntar el fallo después deja pasar
        // a todas las peticiones simultáneas que lean el mismo valor. Así el que recibe el número 16
        // es el decimosexto de verdad, y llega bloqueado.
        //
        // Si la caché lanza se cierra: sin contador no hay tope, y "no lo pude contar" no puede
        // convertirse en "no cuenta".
        var failuresKey = FailuresKey(claims.UserId, claims.Purpose);
        var maxFailures = MaxFailuresFor(claims.Purpose);
        long reserved;
        try
        {
            reserved = await _cache.IncrementAsync(failuresKey, FailureWindow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "2FA: no se pudo apuntar el intento del usuario {UserId} (propósito {Purpose}); se rechaza.",
                claims.UserId, claims.Purpose);
            return Result<ChallengeClaims>.Failure(
                VerificationUnavailable, "No se pudo completar la verificación; inténtelo de nuevo en unos minutos.");
        }

        if (reserved > maxFailures)
        {
            // La reserva no se devuelve: el contador ya pasó el tope y seguir subiéndolo no cambia
            // nada. El reto ni se mira: mientras dure el bloqueo no pasa ni el código bueno, o el
            // bloqueo solo frenaría a quien se equivoca.
            _logger.LogWarning(
                "2FA: verificación rechazada, segundo factor bloqueado para el usuario {UserId} (propósito {Purpose}).",
                claims.UserId, claims.Purpose);
            return Result<ChallengeClaims>.Failure(TwoFactorLocked, LockedMessage);
        }

        var (result, wrongCode) = await VerifyChallengeAsync(claims, code, ct);

        if (!wrongCode)
            await GiveBackFailureAsync(failuresKey, claims, ct);
        else if (reserved == maxFailures)
            // El fallo que llega justo al tope avisa, y solo ese: los siguientes ya no pasan del
            // bloqueo de arriba, así que el aviso sale una vez por ventana.
            await OnLockedAsync(claims, ct);

        return result;
    }

    /// <summary>
    /// La verificación del reto en sí: intentos por reto, formato, código y antirreplay.
    /// <c>WrongCode</c> es true solo cuando se probó un código y no era —incluido uno mal formado—,
    /// que es lo único que cuenta para el tope diario. Un reto agotado o repetido, o un almacén
    /// caído, no son una adivinanza más.
    /// </summary>
    private async Task<(Result<ChallengeClaims> Result, bool WrongCode)> VerifyChallengeAsync(
        ChallengeClaims claims, string code, CancellationToken ct)
    {
        // Los intentos se miran antes que nada: al agotarlos el challenge se quema, y quien
        // insista tiene que enterarse de que el motivo es el límite y no un token cualquiera
        // inválido. Además así el sexto intento falla aunque el código sea el correcto, que es
        // justo lo que impide probar indefinidamente.
        //
        // El intento se apunta al entrar, no al fallar: comprobar sobre un valor leído y
        // escribirlo después no es un tope, porque entre la lectura y la escritura caben
        // todas las peticiones que quepan. Con el incremento atómico, el que recibe el
        // número 6 es el sexto de verdad, aunque los seis lleguen a la vez.
        var attempts = await _cache.IncrementAsync(
            CacheKeys.TwoFactorAttempts(claims.Jti), RemainingLifetime(claims), ct);

        if (attempts > _maxAttemptsPerChallenge)
        {
            await BurnAsync(claims, ct);
            return (Result<ChallengeClaims>.Failure(
                TooManyAttempts, "Demasiados intentos fallidos; solicite un código nuevo."), false);
        }

        // Los tres canales usan seis dígitos ASCII. Cualquier otra cosa es CODE_INVALID sin tocar el
        // HMAC ni Identity, pero contando como intento —por reto y en el tope diario—: si lo mal
        // formado saliera gratis, serviría para tantear el servicio sin gastar nada del límite.
        var verified = IsWellFormedCode(code) &&
            (claims.Channel == TwoFactorChannel.Authenticator
                ? await VerifyAuthenticatorAsync(claims, code)
                : _challenges.VerifyCode(claims, code));

        if (!verified)
        {
            // Este intento ya está contado. Si era el último permitido, el challenge se quema
            // aquí y no en la llamada siguiente: así el atacante no conserva una redención
            // válida guardada para más tarde.
            if (attempts >= _maxAttemptsPerChallenge)
                await BurnAsync(claims, ct);

            return (Result<ChallengeClaims>.Failure(CodeInvalid, "El código introducido no es válido."), true);
        }

        // Antirreplay del challenge: la firma sigue siendo válida hasta su exp, así que sin una
        // marca el mismo token se redimiría dos veces dentro de su ventana de vida.
        //
        // La marca se RECLAMA, no se comprueba y luego se escribe. Con "¿está usado? → verificar →
        // marcar", dos canjes simultáneos del mismo reto leían los dos "no usado" y salían los dos
        // con éxito: dos permisos de step-up por un solo código. El SET NX lo gana uno solo. Va
        // DESPUÉS de verificar el código para que un intento fallido no queme un reto que el
        // usuario todavía puede completar.
        var claim = await ClaimOnceAsync(
            CacheKeys.TwoFactorChallengeConsumed(claims.Jti), RemainingLifetime(claims), claims, "reto", ct);
        if (claim is not null)
            return (claim, false);

        // Antirreplay del código TOTP: Identity lo sigue aceptando durante la tolerancia de reloj
        // (~150 s). En una operación de dinero eso serían dos autorizaciones con el mismo código,
        // cada una con su challenge distinto —el jti no las relaciona—. Mismo trato: se reclama.
        // Quien pierde ve CODE_INVALID, como cualquier código que ya no vale. La clave lleva una
        // etiqueta con clave del servidor y no un hash a secas: quien lea la caché no puede sacar
        // de ella el código.
        if (claims.Channel == TwoFactorChannel.Authenticator)
        {
            claim = await ClaimOnceAsync(
                CacheKeys.TwoFactorTotpUsed(claims.UserId, _challenges.TotpReplayTag(claims.UserId, code)),
                _totpReplayWindow, claims, "código TOTP", ct,
                lostCode: CodeInvalid, lostMessage: "El código introducido no es válido.");
            // Perder esta marca es repetir un código bueno, no adivinar uno: no cuenta como fallo.
            if (claim is not null)
                return (claim, false);
        }

        return (Result<ChallengeClaims>.Success(claims), false);
    }

    /// <summary>Exactamente seis dígitos ASCII: ni espacios, ni dígitos de otros alfabetos.</summary>
    internal static bool IsWellFormedCode(string? code) =>
        code is { Length: 6 } && code.All(char.IsAsciiDigit);

    /// <summary>
    /// Devuelve la reserva del tope diario cuando el intento no fue un código equivocado. Un fallo
    /// de la caché aquí no se propaga: el error queda del lado seguro, un intento de más contado.
    /// </summary>
    private async Task GiveBackFailureAsync(string failuresKey, ChallengeClaims claims, CancellationToken ct)
    {
        try
        {
            await _cache.DecrementAsync(failuresKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "2FA: no se pudo devolver la reserva del contador de fallos del usuario {UserId}; queda un intento de más contado.",
                claims.UserId);
        }
    }

    /// <summary>
    /// Se acaba de alcanzar el tope diario: se deja constancia y se avisa al dueño por correo. El
    /// aviso nunca cambia el resultado —el bloqueo ya está en el contador— y sus fallos no se
    /// propagan.
    /// </summary>
    private async Task OnLockedAsync(ChallengeClaims claims, CancellationToken ct)
    {
        _logger.LogWarning(
            "2FA: tope diario de códigos fallidos alcanzado para el usuario {UserId} (propósito {Purpose}); bloqueado hasta que venza la ventana.",
            claims.UserId, claims.Purpose);

        if (_notifier is null)
        {
            _logger.LogWarning(
                "2FA: sin ISecurityFactorNotifier registrado; no se avisa al usuario {UserId} del bloqueo.", claims.UserId);
            return;
        }

        try
        {
            var user = await _userManager.FindByIdAsync(claims.UserId);
            if (user is not null)
                await _notifier.NotifyTwoFactorLockedAsync(user, claims.Purpose == TwoFactorPurpose.StepUp, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "2FA: no se pudo avisar del bloqueo al usuario {UserId}.", claims.UserId);
        }
    }

    /// <summary>
    /// Reclama una marca de un solo uso. Null si se ganó; si no, el fallo que hay que devolver.
    /// </summary>
    /// <remarks>
    /// <see cref="ICacheService.TryClaimOnceAsync"/> lanza en vez de degradar cuando el almacén no
    /// responde. Aquí se convierte en un fallo —el usuario ve un error, no una excepción— pero
    /// nunca en un éxito: sin marca, el mismo código valdría otra vez.
    /// </remarks>
    private async Task<Result<ChallengeClaims>?> ClaimOnceAsync(
        string key, TimeSpan expiry, ChallengeClaims claims, string what, CancellationToken ct,
        string lostCode = InvalidChallenge, string lostMessage = "Este challenge ya se usó; solicite un código nuevo.")
    {
        try
        {
            if (await _cache.TryClaimOnceAsync(key, expiry, ct))
                return null;

            _logger.LogWarning(
                "2FA: {What} ya usado para el usuario {UserId} (propósito {Purpose}); se rechaza la repetición.",
                what, claims.UserId, claims.Purpose);
            return Result<ChallengeClaims>.Failure(lostCode, lostMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "2FA: no se pudo marcar el {What} como usado para el usuario {UserId} (propósito {Purpose}); se rechaza.",
                what, claims.UserId, claims.Purpose);
            return Result<ChallengeClaims>.Failure(
                VerificationUnavailable, "No se pudo completar la verificación; inténtelo de nuevo en unos minutos.");
        }
    }

    private async Task<bool> VerifyAuthenticatorAsync(ChallengeClaims claims, string code)
    {
        var user = await _userManager.FindByIdAsync(claims.UserId);
        if (user is null)
            return false;

        return await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, code);
    }

    // ── límites y antirreplay ────────────────────────────────────────────────

    /// <summary>
    /// Quema el challenge: agotados los intentos, hay que pedir uno nuevo. Se quema RECLAMANDO la
    /// misma marca que reclama un canje bueno —con Redis, SET NX y las escrituras de
    /// <c>IDistributedCache</c> no comparten espacio de claves, así que una marca escrita con
    /// <c>SetAsync</c> no la vería el reclamo—.
    ///
    /// Si el almacén falla aquí no pasa nada grave: el contador de intentos ya está por encima del
    /// tope y rechaza cualquier intento más por sí solo. Por eso se registra y no se propaga.
    /// </summary>
    private async Task BurnAsync(ChallengeClaims claims, CancellationToken ct)
    {
        // Un reto quemado es alguien que agotó los intentos de un código: o se equivocó cinco veces
        // o está probando. Sin rastro en el log no hay forma de ver el patrón.
        _logger.LogWarning(
            "2FA: reto quemado por intentos agotados para el usuario {UserId} (propósito {Purpose}).",
            claims.UserId, claims.Purpose);

        try
        {
            await _cache.TryClaimOnceAsync(
                CacheKeys.TwoFactorChallengeConsumed(claims.Jti), RemainingLifetime(claims), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "2FA: no se pudo quemar el reto del usuario {UserId}; el límite de intentos sigue rechazándolo.",
                claims.UserId);
        }
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
    /// Primera letra, asteriscos y el dominio intacto. Heredada tal cual del servicio de
    /// challenge que vivía dentro de SignupAPI, para no cambiar lo que ve el usuario ahora que
    /// el login pasa por aquí.
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
