using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using QRCoder;

namespace MLMConquerorGlobalEdition.Authn.Services;

/// <inheritdoc cref="ITotpEnrollmentService"/>
public sealed class TotpEnrollmentService : ITotpEnrollmentService
{
    private const string EnrollmentFailed = "ENROLLMENT_FAILED";
    private const string CodeInvalid      = "CODE_INVALID";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDateTimeProvider             _dateTime;
    private readonly string                        _issuer;

    public TotpEnrollmentService(
        UserManager<ApplicationUser> userManager,
        IDateTimeProvider             dateTime,
        IConfiguration                config)
    {
        _userManager = userManager;
        _dateTime    = dateTime;
        _issuer      = config["Auth:TwoFactor:Issuer"] ?? "MLMConqueror";
    }

    public async Task<Result<TotpEnrollment>> BeginAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var key = await _userManager.GetAuthenticatorKeyAsync(user);

        // Idempotente mientras el enrolamiento sigue abierto: si se regenerase en cada llamada,
        // el QR que el usuario acaba de escanear moriría al recargar la página tras un código
        // erróneo, y volvería a teclear el número de una entrada ya inválida —fallando una y otra
        // vez sin entender por qué—.
        //
        // TwoFactorEnrolledAt es lo que separa los dos casos: lo pone ConfirmAsync al terminar el
        // alta y lo borra ResetAsync. Nulo significa enrolamiento en curso —la clave que hay es la
        // que el usuario está escaneando ahora mismo, hay que conservarla—; no nulo significa que
        // ya hay un autenticador funcionando, y volver por aquí es un re-enrolamiento: ahí la
        // clave tiene que ser nueva, porque devolver la que ya usa dejaría la vieja entrada de la
        // aplicación viva y no habría cambiado nada.
        if (user.TwoFactorEnrolledAt is not null || string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrEmpty(key))
            return Result<TotpEnrollment>.Failure(EnrollmentFailed, "No se pudo generar la clave del autenticador.");

        var uri = $"otpauth://totp/{Uri.EscapeDataString(_issuer)}:{Uri.EscapeDataString(user.Email!)}" +
                  $"?secret={key}&issuer={Uri.EscapeDataString(_issuer)}&digits=6&period=30";

        using var generator = new QRCodeGenerator();
        using var data      = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var png       = new PngByteQRCode(data);
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(png.GetGraphic(10));

        return Result<TotpEnrollment>.Success(new TotpEnrollment(key, uri, dataUri));
    }

    public async Task<Result<bool>> ConfirmAsync(ApplicationUser user, string code, CancellationToken ct = default)
    {
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, TokenOptions.DefaultAuthenticatorProvider, code);

        if (!isValid)
            return Result<bool>.Failure(CodeInvalid, "El código introducido no es válido.");

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        // Now, no UtcNow: se persiste igual que el resto de marcas de negocio del usuario.
        user.TwoFactorEnrolledAt       = _dateTime.Now;
        user.PreferredTwoFactorChannel = TwoFactorChannel.Authenticator;

        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ResetAsync(ApplicationUser user, CancellationToken ct = default)
    {
        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        user.TwoFactorEnrolledAt = null;

        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true);
    }
}
