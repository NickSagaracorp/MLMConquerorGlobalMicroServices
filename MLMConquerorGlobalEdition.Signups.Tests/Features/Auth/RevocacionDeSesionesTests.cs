using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.AccountEnrollment;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.EmailConfirmation;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Logout;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.SetPassword;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.TwoFactorSettings;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// LA TABLA: qué operaciones expulsan a quien ya estaba dentro y cuáles no.
///
/// Un refresh token dura treinta días y sirve para pedir tokens de acceso nuevos sin contraseña y
/// sin segundo factor. La pregunta general —la que originó este archivo— no es "¿revoco al activar
/// el 2FA?" sino "¿QUÉ OPERACIONES CAMBIAN LA POSTURA DE SEGURIDAD DE LA CUENTA?", porque son ésas
/// y solo ésas las que tienen que alcanzar a la sesión que ya estaba abierta.
/// </summary>
/// <remarks>
/// DÓNDE ESTÁ LA LÍNEA:
///
///   • REVOCA lo que cambia QUÉ hace falta para entrar o CON QUÉ se demuestra — contraseña
///     cambiada, restablecida o fijada por primera vez; segundo factor activado o apagado; teléfono
///     confirmado (nace el canal SMS) o retirado (muere).
///
///   • NO REVOCA lo que solo cambia POR DÓNDE llega el código entre factores que ya existían y ya
///     estaban confirmados — el canal preferido —, ni lo que todavía no es un factor — un teléfono
///     dado de alta y sin confirmar —, ni confirmar la dirección de correo que ya identificaba la
///     cuenta.
///
/// POR QUÉ SE PRUEBAN TAMBIÉN LAS QUE **NO** REVOCAN, que es lo que suele faltar: sin esas pruebas,
/// "aquí no se revoca" es indistinguible de "aquí se olvidaron de revocar", y el siguiente que pase
/// añadirá la línea creyendo que arregla algo. Cada una de ellas fija una decisión, no un descuido —
/// y convertir un cambio de preferencia en un cierre de sesión es el otro modo de equivocarse.
///
/// EL HUECO QUE ORIGINÓ TODO ESTO: <c>ConfirmAccountEnrollmentHandler</c> activaba el segundo factor
/// y no revocaba, así que quien tuviera sesión abierta antes de activarlo seguía renovando treinta
/// días sin pasar nunca por el código. Activar el 2FA no expulsaba a quien ya estaba dentro, que son
/// exactamente las sesiones de las que uno se protege al activarlo.
/// </remarks>
public class RevocacionDeSesionesTests
{
    private const string UserId   = "user-001";
    private const string MemberId = "AMB-100001";
    private const string Email    = "usuario@dominio.com";
    private const string Refresco = "REFRESCO-DE-ANTES";

    private static readonly DateTime Caducidad = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Ahora     = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    // ===============================================================================================
    //  Lo que SÍ revoca
    // ===============================================================================================

    /// <summary>
    /// EL HUECO CONOCIDO. Activar el segundo factor desde una sesión ya iniciada tiene que dejar sin
    /// valor el refresco que se emitió cuando la cuenta todavía no lo tenía.
    /// </summary>
    [Fact]
    public async Task ActivarElSegundoFactor_Revoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);

        var enrolamiento = new Mock<ITotpEnrollmentService>();
        enrolamiento.Setup(s => s.ConfirmAsync(user, "123456", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Result<bool>.Success(true));

        var resultado = await new ConfirmAccountEnrollmentHandler(
                userManager.Object, enrolamiento.Object)
            .Handle(new ConfirmAccountEnrollmentCommand(
                UserId, new ConfirmAccountEnrollmentRequest { Code = "123456" }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    /// <summary>
    /// Y con un código MALO no se revoca nada: el enrolamiento no ocurrió, así que la operación no
    /// cambió ninguna postura. Si revocara igual, cualquiera con la sesión de otro tendría en este
    /// endpoint un botón para tirarle la sesión tecleando seis dígitos al azar.
    /// </summary>
    [Fact]
    public async Task ActivarElSegundoFactor_ConCodigoInvalido_NoRevoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);

        var enrolamiento = new Mock<ITotpEnrollmentService>();
        enrolamiento.Setup(s => s.ConfirmAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Result<bool>.Failure("CODE_INVALID", "no vale"));

        var resultado = await new ConfirmAccountEnrollmentHandler(
                userManager.Object, enrolamiento.Object)
            .Handle(new ConfirmAccountEnrollmentCommand(
                UserId, new ConfirmAccountEnrollmentRequest { Code = "999999" }), default);

        resultado.IsSuccess.Should().BeFalse();
        NoDebeHaberRevocado(user);
    }

    [Fact]
    public async Task DesactivarElSegundoFactor_Revoca()
    {
        var user        = Usuario();
        user.TwoFactorEnabled = true;
        var userManager = Gestor(user, "Ambassador");

        var enrolamiento = new Mock<ITotpEnrollmentService>();
        enrolamiento.Setup(s => s.ResetAsync(user, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(Result<bool>.Success(true));

        var resultado = await new DisableTwoFactorHandler(
                userManager.Object, enrolamiento.Object, SinRolesObligatorios())
            .Handle(new DisableTwoFactorCommand(UserId), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    /// <summary>
    /// Rechazada por política de rol, la desactivación NO ocurrió: el segundo factor sigue puesto y
    /// la sesión sigue viva. Revocar aquí castigaría al usuario por una llamada que el servidor
    /// acaba de rechazar.
    /// </summary>
    [Fact]
    public async Task DesactivarElSegundoFactor_CuandoElRolLoProhibe_NoRevoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user, "Admin");

        var resultado = await new DisableTwoFactorHandler(
                userManager.Object, new Mock<ITotpEnrollmentService>().Object, ConRolObligatorio("Admin"))
            .Handle(new DisableTwoFactorCommand(UserId), default);

        resultado.ErrorCode.Should().Be("TWO_FACTOR_REQUIRED");
        NoDebeHaberRevocado(user);
    }

    /// <summary>
    /// Confirmar el teléfono es donde un número se convierte en factor: a partir de aquí el canal
    /// SMS existe para la cuenta. Es el paso que cierra la cadena "sesión robada → mi teléfono →
    /// mis códigos", así que es aquí donde hay que expulsar.
    /// </summary>
    [Fact]
    public async Task ConfirmarElTelefono_Revoca()
    {
        var user = Usuario();
        user.TwoFactorPhoneEncrypted = "ENC:+14155552671";
        user.TwoFactorPhoneLast4     = "2671";

        var userManager = Gestor(user);

        var dosFactores = new Mock<ITwoFactorService>();
        dosFactores.Setup(t => t.VerifyAsync(
                       "reto", "123456", TwoFactorPurpose.Enrollment,
                       It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Result<ChallengeClaims>.Success(new ChallengeClaims(
                       Jti: "jti", UserId: UserId, Email: Email,
                       Purpose: TwoFactorPurpose.Enrollment, OperationKey: null,
                       Channel: TwoFactorChannel.Sms, CodeHash: "hash",
                       IssuedAt: Ahora, ExpiresAt: Ahora.AddMinutes(5))));

        var resultado = await new VerifyPhoneHandler(userManager.Object, dosFactores.Object)
            .Handle(new VerifyPhoneCommand(UserId, new VerifyPhoneRequest
            {
                ChallengeToken = "reto",
                Code           = "123456"
            }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        user.TwoFactorPhoneConfirmed.Should().BeTrue();
        DebeHaberRevocado(user, userManager);
    }

    [Fact]
    public async Task RetirarElTelefono_Revoca()
    {
        var user = Usuario();
        user.TwoFactorPhoneEncrypted = "ENC:+14155552671";
        user.TwoFactorPhoneConfirmed = true;

        var userManager = Gestor(user);

        var resultado = await new RemovePhoneHandler(userManager.Object)
            .Handle(new RemovePhoneCommand(UserId), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    [Fact]
    public async Task CambiarLaContrasena_Revoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);
        userManager.Setup(m => m.ChangePasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);

        using var db = BaseDeDatos();

        var resultado = await new ChangePasswordHandler(userManager.Object, db, accessor.Object)
            .Handle(new ChangePasswordCommand(UserId, new ChangePasswordRequest
            {
                CurrentPassword = "LaDeAntes1!",
                NewPassword     = "LaNueva1A!"
            }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    [Fact]
    public async Task RestablecerLaContrasena_Revoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);
        userManager.Setup(m => m.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var resultado = await new ResetPasswordHandler(userManager.Object)
            .Handle(new ResetPasswordCommand(new ResetPasswordRequest
            {
                UserId      = UserId,
                Token       = Base64Url("un-token"),
                NewPassword = "LaNueva1A!"
            }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    [Fact]
    public async Task FijarLaPrimeraContrasena_Revoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);
        userManager.Setup(m => m.HasPasswordAsync(user)).ReturnsAsync(false);
        userManager.Setup(m => m.AddPasswordAsync(user, It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var resultado = await new SetPasswordHandler(userManager.Object)
            .Handle(new SetPasswordCommand(
                UserId, new SetPasswordRequest { NewPassword = "LaNueva1A!" }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    [Fact]
    public async Task CerrarSesion_Revoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);

        var resultado = await new LogoutHandler(userManager.Object)
            .Handle(new LogoutCommand(UserId), default);

        resultado.IsSuccess.Should().BeTrue();
        DebeHaberRevocado(user, userManager);
    }

    /// <summary>
    /// Sustituir un teléfono YA CONFIRMADO sí revoca: la cuenta pierde el factor SMS que tenía —el
    /// número nuevo nace sin confirmar—, que es lo mismo que hace retirarlo. Lo que marca la línea
    /// es lo que la cuenta pierde, no la ruta que se llamó.
    /// </summary>
    [Fact]
    public async Task SustituirUnTelefonoYaConfirmado_Revoca()
    {
        var user = Usuario();
        user.TwoFactorPhoneEncrypted = "ENC:+14155550000";
        user.TwoFactorPhoneLast4     = "0000";
        user.TwoFactorPhoneConfirmed = true;

        var (handler, userManager) = AltaDeTelefono(user);

        var resultado = await handler.Handle(new AddPhoneCommand(
            UserId, new AddPhoneRequest { PhoneE164 = "+14155552671" }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    // ===============================================================================================
    //  Lo que NO revoca — y cada una de estas pruebas fija una decisión, no tapa un olvido
    // ===============================================================================================

    /// <summary>
    /// DONDE SE PONE LA LÍNEA. Cambiar el canal preferido no añade ni quita ningún factor: los
    /// canales elegibles ya estaban confirmados y ya podían recibir un código de esta cuenta. Lo
    /// único que cambia es por cuál de ellos llega. Revocar aquí convertiría un ajuste de
    /// preferencia —el más frecuente de todos— en un cierre de sesión.
    /// </summary>
    [Fact]
    public async Task CambiarElCanalPreferido_NoRevoca()
    {
        var user = Usuario();
        user.TwoFactorPhoneConfirmed = true;
        user.TwoFactorPhoneEncrypted = "ENC:+14155552671";

        var userManager = Gestor(user);

        var resultado = await new SetTwoFactorChannelHandler(userManager.Object)
            .Handle(new SetTwoFactorChannelCommand(
                UserId, new SetTwoFactorChannelRequest { Channel = TwoFactorChannel.Sms }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Sms);
        NoDebeHaberRevocado(user);
    }

    /// <summary>
    /// Dar de alta un teléfono NUEVO en una cuenta que no tenía ninguno confirmado tampoco revoca:
    /// el número queda con <c>TwoFactorPhoneConfirmed = false</c> y hasta que se redima su código no
    /// es un factor ni abre nada. El momento de expulsar es la confirmación, no el alta.
    /// </summary>
    [Fact]
    public async Task DarDeAltaUnTelefonoSinConfirmar_NoRevoca()
    {
        var user = Usuario();
        var (handler, _) = AltaDeTelefono(user);

        var resultado = await handler.Handle(new AddPhoneCommand(
            UserId, new AddPhoneRequest { PhoneE164 = "+14155552671" }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        user.TwoFactorPhoneConfirmed.Should().BeFalse();
        NoDebeHaberRevocado(user);
    }

    /// <summary>
    /// Confirmar la dirección de correo tampoco revoca: no la CAMBIA, solo acredita la que ya
    /// identificaba la cuenta desde el alta y a la que ya iban los códigos por correo. Nada se mueve
    /// de sitio. (Cambiarla es otra operación y vive en BizCenter — ésa sí revoca.)
    /// </summary>
    [Fact]
    public async Task ConfirmarLaDireccionDeCorreo_NoRevoca()
    {
        var user        = Usuario();
        var userManager = Gestor(user);
        userManager.Setup(m => m.ConfirmEmailAsync(user, It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var resultado = await new ConfirmEmailHandler(userManager.Object)
            .Handle(new ConfirmEmailCommand(new ConfirmEmailRequest
            {
                UserId = UserId,
                Token  = Base64Url("un-token")
            }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        NoDebeHaberRevocado(user);
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    /// <summary>Una cuenta CON sesión viva: si no la tuviera, no habría nada que revocar y las dos
    /// afirmaciones —revoca y no revoca— se verían igual sobre los campos ya nulos.</summary>
    private static ApplicationUser Usuario() => new()
    {
        Id                        = UserId,
        Email                     = Email,
        IsActive                  = true,
        MemberProfileId           = MemberId,
        PreferredTwoFactorChannel = TwoFactorChannel.Email,
        RefreshToken              = Refresco,
        RefreshTokenExpiry        = Caducidad
    };

    private static Mock<UserManager<ApplicationUser>> Gestor(
        ApplicationUser user, params string[] roles)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                   .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.SetTwoFactorEnabledAsync(user, It.IsAny<bool>()))
                   .ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static (AddPhoneHandler Handler, Mock<UserManager<ApplicationUser>> Gestor) AltaDeTelefono(
        ApplicationUser user)
    {
        var userManager = Gestor(user);

        var cifrado = new Mock<IEncryptionService>();
        cifrado.Setup(c => c.Encrypt(It.IsAny<string>())).Returns<string>(v => "ENC:" + v);

        var dosFactores = new Mock<ITwoFactorService>();
        dosFactores.Setup(t => t.IssueAsync(
                       It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(),
                       It.IsAny<string?>(), It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(),
                       It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Result<ChallengeIssued>.Success(new ChallengeIssued(
                       ChallengeToken: "reto",
                       Channel:        TwoFactorChannel.Sms,
                       MaskedTarget:   "***2671",
                       ExpiresAt:      Ahora.AddMinutes(5))));

        return (new AddPhoneHandler(
                    userManager.Object, cifrado.Object, dosFactores.Object, BaseDeDatos()),
                userManager);
    }

    private static AppDbContext BaseDeDatos() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration SinRolesObligatorios() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    private static IConfiguration ConRolObligatorio(string rol) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Auth:TwoFactor:MandatoryRoles:0"] = rol }).Build();

    private static string Base64Url(string valor) =>
        Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes(valor));

    /// <summary>
    /// Revocar es DEJAR LOS DOS CAMPOS NULOS Y GUARDARLO. Comprobar solo el objeto en memoria
    /// dejaría pasar una revocación que nunca llega a la base, que es no revocar nada.
    /// </summary>
    private static void DebeHaberRevocado(
        ApplicationUser user, Mock<UserManager<ApplicationUser>> userManager)
    {
        user.RefreshToken.Should().BeNull(
            "un refresco vivo renueva la sesión treinta días sin volver a pasar por ninguna " +
            "credencial: la operación que acaba de cambiar la postura de seguridad de la cuenta " +
            "tiene que alcanzarlo");
        user.RefreshTokenExpiry.Should().BeNull();

        userManager.Verify(m => m.UpdateAsync(user), Times.AtLeastOnce);
    }

    private static void NoDebeHaberRevocado(ApplicationUser user)
    {
        user.RefreshToken.Should().Be(Refresco,
            "esta operación no cambia qué hace falta para entrar en la cuenta, así que cerrar la " +
            "sesión sería una molestia sin nada que proteger");
        user.RefreshTokenExpiry.Should().Be(Caducidad);
    }
}
