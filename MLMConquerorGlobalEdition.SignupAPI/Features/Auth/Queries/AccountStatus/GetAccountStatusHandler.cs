using MediatR;
using Microsoft.AspNetCore.Identity;
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

            // La regla vive en TwoFactorChannelAvailability y no aquí: el comando que fija el
            // canal preferido tiene que rechazar exactamente lo que esta lista no ofrece, y con
            // dos copias de la misma condición basta con que una se quede atrás para que el
            // servidor acepte un canal sin destino.
            AvailableChannels = TwoFactorChannelAvailability.Resolve(user)
        });
    }
}
