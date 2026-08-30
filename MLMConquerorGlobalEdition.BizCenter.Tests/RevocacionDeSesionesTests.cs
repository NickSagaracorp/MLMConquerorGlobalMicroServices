using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdateEmail;
using MLMConquerorGlobalEdition.BizCenter.Features.Profile.UpdatePassword;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// LA MITAD DE LA TABLA QUE VIVE EN BIZCENTER: las dos operaciones de la pestaña de credenciales del
/// perfil del miembro. La otra mitad —el segundo factor, el teléfono y las tres contraseñas de
/// SignupAPI— está en <c>RevocacionDeSesionesTests</c> de Signups.Tests, con la regla completa.
/// </summary>
/// <remarks>
/// POR QUÉ ESTAS DOS ESTABAN FUERA DE LA REGLA, y es el mismo motivo en las dos: son la SEGUNDA
/// puerta a algo que ya tenía una. La contraseña se puede cambiar por
/// <c>PUT /api/v1/auth/change-password</c> (SignupAPI, que revocaba) y por
/// <c>PUT /api/v1/bizcenter/profile/credentials/password</c> (esta, que no). Una puerta que revoca y
/// otra que no es peor que ninguna de las dos: el miembro cree haber expulsado a quien tuviera su
/// contraseña vieja y, según por qué pantalla haya entrado, no ha expulsado a nadie.
///
/// Y EL CORREO ES EL CASO MÁS GRAVE de toda la familia, porque no es un dato de contacto: es el
/// identificador con el que se inicia sesión, el destino del enlace de recuperación de contraseña y
/// el canal de segundo factor que siempre está disponible. Cambiarlo reapunta las tres cosas a la
/// vez, así que una sesión robada que sobreviviera a este cambio sería la cuenta entera.
/// </remarks>
public class RevocacionDeSesionesTests
{
    private const string UserId    = "user-001";
    private const string MemberId  = "AMB-100001";
    private const string CorreoOld = "antiguo@dominio.com";
    private const string Refresco  = "REFRESCO-DE-ANTES";

    private static readonly DateTime Caducidad = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CambiarElCorreo_Revoca()
    {
        var user = Usuario();
        using var db = BaseDeDatos();

        var userManager = Gestor(db, user);
        userManager.Setup(m => m.SetEmailAsync(user, It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.SetUserNameAsync(user, It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var resultado = await new UpdateEmailHandler(
                db, UsuarioActual(), Reloj(), userManager.Object, SinContexto())
            .Handle(new UpdateEmailCommand(
                new UpdateEmailRequest { NewEmail = "nuevo@dominio.com" }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    /// <summary>
    /// Un cambio que la API rechaza no revoca nada: el correo sigue siendo el mismo, así que la
    /// postura de seguridad de la cuenta no se ha movido. Si revocara igual, este endpoint sería un
    /// botón para tirarle la sesión a alguien mandando el correo de otro.
    /// </summary>
    [Fact]
    public async Task CambiarElCorreo_CuandoElNuevoYaEstaEnUso_NoRevoca()
    {
        var user = Usuario();
        using var db = BaseDeDatos();

        var otro = new ApplicationUser
        {
            Id              = "user-002",
            Email           = "nuevo@dominio.com",
            NormalizedEmail = "NUEVO@DOMINIO.COM"
        };

        var userManager = Gestor(db, user, otro);

        var resultado = await new UpdateEmailHandler(
                db, UsuarioActual(), Reloj(), userManager.Object, SinContexto())
            .Handle(new UpdateEmailCommand(
                new UpdateEmailRequest { NewEmail = "nuevo@dominio.com" }), default);

        resultado.ErrorCode.Should().Be("EMAIL_TAKEN");
        NoDebeHaberRevocado(user);
    }

    [Fact]
    public async Task CambiarLaContrasena_Revoca()
    {
        var user = Usuario();
        using var db = BaseDeDatos();

        var userManager = Gestor(db, user);
        userManager.Setup(m => m.ChangePasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var resultado = await new UpdatePasswordHandler(
                db, UsuarioActual(), Reloj(), userManager.Object, SinContexto())
            .Handle(new UpdatePasswordCommand(new UpdatePasswordRequest
            {
                CurrentPassword = "LaDeAntes1!",
                NewPassword     = "LaNueva1A!"
            }), default);

        resultado.IsSuccess.Should().BeTrue(because: resultado.Error);
        DebeHaberRevocado(user, userManager);
    }

    /// <summary>
    /// Con la contraseña actual mal, Identity rechaza el cambio y no hay nada que revocar: la
    /// credencial de la cuenta sigue siendo la misma. Revocar aquí daría a cualquiera con la ruta a
    /// mano una forma de cerrarle la sesión al dueño sin saber su contraseña.
    /// </summary>
    [Fact]
    public async Task CambiarLaContrasena_ConLaActualMal_NoRevoca()
    {
        var user = Usuario();
        using var db = BaseDeDatos();

        var userManager = Gestor(db, user);
        userManager.Setup(m => m.ChangePasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "no coincide" }));

        var resultado = await new UpdatePasswordHandler(
                db, UsuarioActual(), Reloj(), userManager.Object, SinContexto())
            .Handle(new UpdatePasswordCommand(new UpdatePasswordRequest
            {
                CurrentPassword = "LaQueNoEs1!",
                NewPassword     = "LaNueva1A!"
            }), default);

        resultado.ErrorCode.Should().Be("PASSWORD_CHANGE_REJECTED");
        NoDebeHaberRevocado(user);
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    private static ApplicationUser Usuario() => new()
    {
        Id                 = UserId,
        Email              = CorreoOld,
        NormalizedEmail    = CorreoOld.ToUpperInvariant(),
        UserName           = CorreoOld,
        IsActive           = true,
        MemberProfileId    = MemberId,
        RefreshToken       = Refresco,
        RefreshTokenExpiry = Caducidad
    };

    /// <summary>
    /// El gestor con <c>Users</c> apuntando al <c>DbSet</c> del contexto en memoria. Tiene que ser
    /// una consulta de EF y no una lista: los dos manejadores resuelven al usuario con
    /// <c>FirstOrDefaultAsync(u =&gt; u.MemberProfileId == memberId)</c>, y un
    /// <c>IQueryable</c> corriente no admite las operaciones asíncronas de EF.
    /// </summary>
    private static Mock<UserManager<ApplicationUser>> Gestor(
        AppDbContext db, params ApplicationUser[] usuarios)
    {
        db.Users.AddRange(usuarios);
        db.SaveChanges();

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(db.Users);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                   .ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private static ICurrentUserService UsuarioActual()
    {
        var actual = new Mock<ICurrentUserService>();
        actual.SetupGet(c => c.MemberId).Returns(MemberId);
        actual.SetupGet(c => c.UserId).Returns(UserId);
        return actual.Object;
    }

    private static IDateTimeProvider Reloj()
    {
        var reloj = new Mock<IDateTimeProvider>();
        var ahora = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        reloj.SetupGet(r => r.Now).Returns(ahora);
        reloj.SetupGet(r => r.UtcNow).Returns(ahora);
        return reloj.Object;
    }

    private static IHttpContextAccessor SinContexto()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns((HttpContext?)null);
        return accessor.Object;
    }

    private static AppDbContext BaseDeDatos() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void DebeHaberRevocado(
        ApplicationUser user, Mock<UserManager<ApplicationUser>> userManager)
    {
        user.RefreshToken.Should().BeNull(
            "un refresco vivo renueva la sesión treinta días sin volver a pasar por ninguna " +
            "credencial, y esta operación acaba de cambiar cuál es esa credencial");
        user.RefreshTokenExpiry.Should().BeNull();
        userManager.Verify(m => m.UpdateAsync(user), Times.AtLeastOnce);
    }

    private static void NoDebeHaberRevocado(ApplicationUser user)
    {
        user.RefreshToken.Should().Be(Refresco, "la operación no llegó a ocurrir");
        user.RefreshTokenExpiry.Should().Be(Caducidad);
    }

    /// <summary>
    /// Un <c>UserManager</c> de Moq. Es la misma fábrica que Signups.Tests: se repite aquí porque los
    /// dos proyectos de prueba no comparten ensamblado y no hay uno de utilidades común.
    /// </summary>
    private static class UserManagerHelper
    {
        public static Mock<UserManager<ApplicationUser>> Create()
        {
            var store  = new Mock<IUserStore<ApplicationUser>>();
            var hasher = new Mock<IPasswordHasher<ApplicationUser>>();

            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null, hasher.Object,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                null, null, null, null);
        }
    }
}
