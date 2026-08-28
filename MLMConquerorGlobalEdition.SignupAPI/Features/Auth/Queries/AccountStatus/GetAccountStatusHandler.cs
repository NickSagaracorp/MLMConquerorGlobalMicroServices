using MediatR;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.AccountStatus;

/// <summary>
/// Estado de la cuenta del usuario autenticado: lo que el panel de gestión necesita para
/// pintarse de una sola llamada.
/// </summary>
/// <remarks>
/// El teléfono sale solo enmascarado, desde <c>TwoFactorPhoneLast4</c>. Ver
/// <see cref="AccountMasking"/> para por qué no se descifra el número entero.
/// </remarks>
public class GetAccountStatusHandler : IRequestHandler<GetAccountStatusQuery, Result<AccountStatusResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public GetAccountStatusHandler(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public async Task<Result<AccountStatusResponse>> Handle(GetAccountStatusQuery query, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(query.UserId);
        if (user is null || !user.IsActive)
            return Result<AccountStatusResponse>.Failure("USER_NOT_FOUND", "User not found.");

        // Se pregunta a Identity en vez de mirar el hash: PasswordHash no es asunto de esta capa
        // y no tiene por qué salir del almacén para responder un booleano.
        var hasPassword = await _userManager.HasPasswordAsync(user);

        var hasPhone = !string.IsNullOrWhiteSpace(user.TwoFactorPhoneEncrypted);

        return Result<AccountStatusResponse>.Success(new AccountStatusResponse
        {
            Email          = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,

            MaskedPhone    = AccountMasking.MaskPhoneFromLast4(user.TwoFactorPhoneLast4),
            HasPhone       = hasPhone,
            PhoneConfirmed = user.TwoFactorPhoneConfirmed,

            TwoFactorEnabled          = user.TwoFactorEnabled,
            PreferredTwoFactorChannel = user.PreferredTwoFactorChannel,
            TwoFactorEnrolledAt       = user.TwoFactorEnrolledAt,

            HasPassword       = hasPassword,
            AvailableChannels = ResolveAvailableChannels(user, hasPhone)
        });
    }

    /// <summary>
    /// Canales que tienen destino real para este usuario. Mismas condiciones que aplica
    /// <c>ResolveTarget</c> de la librería de 2FA al emitir el código: la pantalla no puede
    /// ofrecer un canal que luego devolvería CHANNEL_UNAVAILABLE, porque el usuario elegiría ese
    /// canal, no recibiría nada y se quedaría fuera en su siguiente inicio de sesión.
    /// </summary>
    private static IReadOnlyList<TwoFactorChannel> ResolveAvailableChannels(
        ApplicationUser user, bool hasPhone)
    {
        // Correo siempre: es lo que identifica la cuenta, así que su destino existe por definición.
        var channels = new List<TwoFactorChannel> { TwoFactorChannel.Email };

        // SMS solo con el teléfono confirmado. Un número que nadie ha demostrado tener no es un
        // segundo factor, y la librería lo rechaza fuera del enrolamiento.
        if (hasPhone && user.TwoFactorPhoneConfirmed)
            channels.Add(TwoFactorChannel.Sms);

        // Authenticator solo con el enrolamiento confirmado: sin clave dada de alta no hay nada
        // que Identity pueda verificar y la pantalla del código no aceptaría ninguno.
        if (user.TwoFactorEnrolledAt is not null)
            channels.Add(TwoFactorChannel.Authenticator);

        return channels;
    }
}
