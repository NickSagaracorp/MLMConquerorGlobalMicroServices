using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ChangePassword;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Cambio de contraseña de una cuenta que ya tiene una — el único camino que queda para cambiarla
/// desde cualquiera de los dos portales.
///
/// POR QUÉ HAY AQUÍ PRUEBAS DE HISTORIAL. El centro de negocios tenía su propio formulario en la
/// pestaña "Cuenta" del perfil, posteando a PUT /api/v1/bizcenter/profile/credentials/password. Ese
/// manejador escribía además una fila en MemberCredentialChangeLogs, que es lo que pinta
/// CredentialsHistoryTable justo al lado — el sitio donde un miembro puede ver un cambio de
/// contraseña que no hizo. Al quitar aquel formulario duplicado, esa escritura tenía que subir al
/// camino compartido ANTES de borrar nada, o el historial se habría quedado mudo justo para el
/// suceso que más importa vigilar.
/// </summary>
public class ChangePasswordHandlerTests
{
    private const string UserId          = "user-001";
    private const string MemberId        = "AMB-100001";
    private const string CurrentPassword = "LaDeAntes1!";
    private const string NewPassword     = "LaNueva1A!";

    private static ApplicationUser User(string? memberProfileId = MemberId, bool active = true) => new()
    {
        Id                 = UserId,
        Email              = "miembro@dominio.com",
        IsActive           = active,
        MemberProfileId    = memberProfileId,
        RefreshToken       = "REFRESH-VIEJO",
        RefreshTokenExpiry = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Mock<UserManager<ApplicationUser>> UserManagerWith(
        ApplicationUser? user, IdentityResult? changeResult = null)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userManager.Setup(m => m.ChangePasswordAsync(
                       It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync(changeResult ?? IdentityResult.Success);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                   .ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static ChangePasswordHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext                       db,
        HttpContext?                       httpContext = null)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(httpContext);

        return new ChangePasswordHandler(userManager.Object, db, accessor.Object);
    }

    private static ChangePasswordCommand Command() =>
        new(UserId, new ChangePasswordRequest
        {
            CurrentPassword = CurrentPassword,
            NewPassword     = NewPassword
        });

    private static DefaultHttpContext PeticionDesde(string ip, string userAgent)
    {
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        contexto.Request.Headers.UserAgent  = userAgent;
        return contexto;
    }

    // ===========================================================================================
    //  Lo de siempre: la contraseña cambia y las sesiones abiertas no la sobreviven
    // ===========================================================================================

    [Fact]
    public async Task ChangePassword_ConLaActualCorrecta_LaCambiaYInvalidaElRefresco()
    {
        var user        = User();
        var userManager = UserManagerWith(user);
        using var db    = InMemoryDbHelper.Create();

        var result = await BuildHandler(userManager, db).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        userManager.Verify(m => m.ChangePasswordAsync(user, CurrentPassword, NewPassword), Times.Once);

        // Un cambio de contraseña tiene que cerrar las sesiones que ya estaban abiertas: si no, a
        // quien se la robó le da igual que su dueño la cambie.
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_ConLaActualEquivocada_FallaYNoTocaNada()
    {
        var user        = User();
        var userManager = UserManagerWith(user, IdentityResult.Failed(
            new IdentityError { Description = "Incorrect password." }));
        using var db    = InMemoryDbHelper.Create();

        var result = await BuildHandler(userManager, db).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PASSWORD_CHANGE_FAILED");

        user.RefreshToken.Should().Be("REFRESH-VIEJO");
        db.MemberCredentialChangeLogs.Should().BeEmpty(
            "un cambio que no ocurrió no puede aparecer en el historial del miembro");
    }

    [Fact]
    public async Task ChangePassword_ConLaCuentaDesactivada_NoLaCambia()
    {
        var userManager = UserManagerWith(User(active: false));
        using var db    = InMemoryDbHelper.Create();

        var result = await BuildHandler(userManager, db).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        db.MemberCredentialChangeLogs.Should().BeEmpty();
    }

    // ===========================================================================================
    //  El historial de credenciales, que subió aquí al unificar la pantalla
    // ===========================================================================================

    [Fact]
    public async Task ChangePassword_DejaConstanciaEnElHistorialDelMiembro()
    {
        var userManager = UserManagerWith(User());
        using var db    = InMemoryDbHelper.Create();

        var peticion = PeticionDesde("203.0.113.7", "Mozilla/5.0 (pruebas)");

        var result = await BuildHandler(userManager, db, peticion)
            .Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var fila = db.MemberCredentialChangeLogs.Should().ContainSingle().Subject;

        fila.MemberId.Should().Be(MemberId);
        fila.Kind.Should().Be(CredentialChangeKind.Password);
        fila.IpAddress.Should().Be("203.0.113.7");
        fila.UserAgent.Should().Be("Mozilla/5.0 (pruebas)");
        fila.CreatedBy.Should().Be(UserId);

        // La fecha la sella AuditInterceptor con la hora del SERVIDOR justo antes de guardar, no el
        // manejador: por eso se comprueba que sea hora local y reciente, y no un valor fijado desde
        // la prueba. Un valor escrito a mano en el manejador se perdería aquí sin dejar rastro.
        fila.CreationDate.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// La contraseña NUNCA entra en el historial, ni la vieja ni la nueva. La tabla guarda que el
    /// cambio ocurrió, cuándo y desde dónde; los dos campos de valor existen solo para el cambio de
    /// correo, donde el dato no es un secreto.
    /// </summary>
    [Fact]
    public async Task ChangePassword_NoGuardaNingunaContrasenaEnElHistorial()
    {
        var userManager = UserManagerWith(User());
        using var db    = InMemoryDbHelper.Create();

        await BuildHandler(userManager, db).Handle(Command(), CancellationToken.None);

        var fila = db.MemberCredentialChangeLogs.Single();

        fila.PreviousValue.Should().BeNull();
        fila.NewValue.Should().BeNull();
    }

    /// <summary>
    /// El personal interno no tiene MemberProfile, y la tabla se indexa por MemberId: para ellos no
    /// hay fila que escribir ni pantalla donde leerla. Escribirla con el identificador vacío
    /// ensuciaría el historial de nadie con filas que no se pueden atribuir.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ChangePassword_DeUnaCuentaSinPerfilDeMiembro_NoEscribeHistorial(string? memberId)
    {
        var userManager = UserManagerWith(User(memberProfileId: memberId));
        using var db    = InMemoryDbHelper.Create();

        var result = await BuildHandler(userManager, db).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        db.MemberCredentialChangeLogs.Should().BeEmpty();
    }

    /// <summary>
    /// Sin HttpContext —una llamada de fondo, una prueba— el cambio sigue anotándose: la IP y el
    /// agente son contexto forense útil, no requisitos. Perder la fila entera por no tener una
    /// cabecera sería perder justo lo que se quiere conservar.
    /// </summary>
    [Fact]
    public async Task ChangePassword_SinContextoDePeticion_AnotaIgualConLosDatosQueTiene()
    {
        var userManager = UserManagerWith(User());
        using var db    = InMemoryDbHelper.Create();

        await BuildHandler(userManager, db, httpContext: null)
            .Handle(Command(), CancellationToken.None);

        var fila = db.MemberCredentialChangeLogs.Should().ContainSingle().Subject;
        fila.MemberId.Should().Be(MemberId);
        fila.IpAddress.Should().BeNull();
    }
}
