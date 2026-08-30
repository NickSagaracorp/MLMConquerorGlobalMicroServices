using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;

/// <summary>
/// Da de alta el teléfono del canal SMS y le manda el código que lo confirmará.
///
/// El número queda guardado con <c>TwoFactorPhoneConfirmed = false</c>: hasta que alguien
/// demuestre tenerlo redimiendo el código, no es un segundo factor y ningún login lo usa.
/// </summary>
/// <remarks>
/// El SMS sale por <see cref="ITwoFactorService"/> y no por <c>ISmsService</c>. No es una
/// preferencia de estilo: este endpoint acepta un número arbitrario del cuerpo de la petición, y
/// la librería es la que aplica el tope de tres emisiones cada quince minutos por usuario.
/// Llamando al transporte directamente el tope no existiría, y el endpoint se convertiría en una
/// herramienta para mandar tantos SMS como se quisiera a cualquier teléfono del mundo — cada uno
/// facturado por Twilio a la empresa.
/// </remarks>
public class AddPhoneHandler : IRequestHandler<AddPhoneCommand, Result<PhoneChallengeResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEncryptionService           _encryption;
    private readonly ITwoFactorService            _twoFactor;
    private readonly AppDbContext                 _db;

    public AddPhoneHandler(
        UserManager<ApplicationUser> userManager,
        IEncryptionService           encryption,
        ITwoFactorService            twoFactor,
        AppDbContext                 db)
    {
        _userManager = userManager;
        _encryption  = encryption;
        _twoFactor   = twoFactor;
        _db          = db;
    }

    public async Task<Result<PhoneChallengeResponse>> Handle(AddPhoneCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null || !user.IsActive)
            return Result<PhoneChallengeResponse>.Failure("USER_NOT_FOUND", "User not found.");

        var phone = command.Request.PhoneE164?.Trim() ?? string.Empty;

        // Misma regla que aplica el transporte de SMS, no una copia: un número aceptado aquí y
        // rechazado en Twilio dejaría al usuario esperando un código que reventó por dentro. Se
        // corta antes de tocar nada, para que un número mal escrito no gaste cupo de emisiones.
        if (!PhoneNumberFormat.IsE164(phone))
            return Result<PhoneChallengeResponse>.Failure(
                "INVALID_PHONE",
                "El teléfono debe estar en formato E.164: '+' y de 8 a 15 dígitos, sin espacios.");

        // El teléfono se guarda cifrado —es PII y a la vez factor de autenticación— y los cuatro
        // últimos dígitos aparte, en claro a propósito: son los que permiten enmascararlo en
        // pantalla sin descifrar el número en cada carga.
        user.TwoFactorPhoneEncrypted = _encryption.Encrypt(phone);
        user.TwoFactorPhoneLast4     = phone[^4..];

        // Sin confirmar, siempre. Si se está sustituyendo un número ya verificado, la marca del
        // anterior no puede heredarse: nadie ha demostrado todavía tener este.
        var replacedConfirmedPhone   = user.TwoFactorPhoneConfirmed;
        user.TwoFactorPhoneConfirmed = false;

        // Sustituir un teléfono confirmado teniendo SMS como canal preferido deja ese canal sin
        // destino: el siguiente inicio de sesión pediría un código por SMS, ResolveTarget
        // devolvería null y la cuenta se quedaría fuera con CHANNEL_UNAVAILABLE. Vuelve a correo
        // —que siempre existe, porque identifica la cuenta— hasta que el número nuevo se
        // confirme. Mismo criterio que al dar de baja el teléfono.
        if (replacedConfirmedPhone && user.PreferredTwoFactorChannel == TwoFactorChannel.Sms)
            user.PreferredTwoFactorChannel = TwoFactorChannel.Email;

        // Y SOLO EN ESE CASO SE REVOCA LA SESIÓN VIVA. Dar de alta un número nuevo no toca la
        // postura de seguridad de la cuenta —queda sin confirmar, no abre nada— pero SUSTITUIR uno
        // que sí estaba confirmado retira un factor que existía, que es exactamente lo que hace
        // RemovePhoneHandler. La línea la marca lo que la cuenta pierde, no la ruta que se llamó.
        if (replacedConfirmedPhone)
            user.RevokeLiveSessions();

        await _userManager.UpdateAsync(user);

        var languageCode = await ResolveLanguageAsync(user, ct);

        // Canal forzado a SMS: lo que hay que verificar es este teléfono, no el canal que el
        // usuario prefiera. Propósito Enrollment: es el único con el que la librería resuelve un
        // SMS todavía sin confirmar, que es exactamente la situación de huevo y gallina que este
        // endpoint tiene que resolver.
        var issued = await _twoFactor.IssueAsync(
            user,
            TwoFactorPurpose.Enrollment,
            forcedChannel: TwoFactorChannel.Sms,
            languageCode:  languageCode,
            ct:            ct);

        // El tope de emisiones y las caídas del transporte se propagan tal cual: quien recibe
        // TOO_MANY_REQUESTS tiene que enterarse, en vez de ver un éxito por un SMS que no salió.
        // El número queda guardado sin confirmar, así que no abre ningún canal; basta con
        // reintentar el alta.
        if (!issued.IsSuccess)
            return Result<PhoneChallengeResponse>.Failure(issued.ErrorCode!, issued.Error!);

        return Result<PhoneChallengeResponse>.Success(new PhoneChallengeResponse
        {
            ChallengeToken = issued.Value!.ChallengeToken,
            MaskedTarget   = issued.Value.MaskedTarget,
            ExpiresAt      = issued.Value.ExpiresAt
        });
    }

    /// <summary>
    /// Idioma de la plantilla del SMS. El personal no tiene MemberProfile: devuelve null y la
    /// librería cae a "en".
    /// </summary>
    private async Task<string?> ResolveLanguageAsync(ApplicationUser user, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(user.MemberProfileId))
            return null;

        var language = await _db.MemberProfiles.AsNoTracking()
            .Where(m => m.MemberId == user.MemberProfileId)
            .Select(m => m.DefaultLanguage)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(language) ? null : language;
    }
}
