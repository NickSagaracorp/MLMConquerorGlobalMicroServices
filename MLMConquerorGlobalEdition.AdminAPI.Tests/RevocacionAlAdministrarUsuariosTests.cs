using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// ADMINISTRAR UNA CUENTA DE PERSONAL TIENE QUE EXPULSAR A QUIEN YA ESTABA DENTRO.
/// </summary>
/// <remarks>
/// LO QUE HABÍA. Las cuatro escrituras de <c>SystemUsersController</c> cambian la postura de
/// seguridad de una cuenta —desactivarla, darla de baja, cambiarle el correo y cambiarle los
/// roles— y ninguna tocaba el refresh token. Ese token vive treinta días y sirve para pedir tokens
/// de acceso nuevos SIN contraseña y SIN segundo factor: la cuenta recién desactivada seguía
/// renovándose sola.
///
/// LA REGLA ES LA DE <c>SessionRevocation</c>, la misma que rige desde 4f4beaf en el área de
/// cuenta: se revoca cuando cambia QUÉ hace falta para entrar o CON QUÉ se demuestra. Aquí eso
/// significa desactivar, borrar, cambiar el correo —identificador de acceso, destino de la
/// recuperación y canal de 2FA siempre disponible— y cambiar los roles.
///
/// Y NO SE REVOCA CUANDO NO CAMBIA NADA. Abrir el formulario y darle a guardar con los mismos
/// valores no es un cambio de postura; tratarlo como tal convertiría un clic inofensivo en un
/// cierre de sesión, y esa es la mitad de la regla que se olvida siempre.
///
/// LO QUE ESTO NO ALCANZA: el token de acceso ya emitido, que es autofirmado y vive lo que diga
/// <c>Jwt:AccessTokenExpiryMinutes</c>. Está descrito en el <c>&lt;remarks&gt;</c> del controlador.
/// </remarks>
public class RevocacionAlAdministrarUsuariosTests
{
    private const string Id = "staff-001";

    [Fact]
    public async Task CambiarElCorreo_RevocaLaSesionViva()
    {
        var (control, usuario, _) = Escenario(correo: "viejo@casa.com", roles: [AppRoles.Admin]);

        await control.Update(Id, new SystemUsersController.UpdateSystemUserRequest(
            "nuevo@casa.com", AppRoles.Admin, IsActive: true));

        DebeEstarRevocada(usuario,
            "el correo es el identificador de acceso, el destino del enlace de recuperación y el " +
            "canal de 2FA que siempre está disponible");
    }

    [Fact]
    public async Task CambiarLosRoles_RevocaLaSesionViva()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.Update(Id, new SystemUsersController.UpdateSystemUserRequest(
            "staff@casa.com", AppRoles.SupportLevel1, IsActive: true));

        DebeEstarRevocada(usuario,
            "el refresco relee los roles: sin revocar, la cuenta sigue pidiendo tokens nuevos sin " +
            "volver a demostrar nada");
    }

    [Fact]
    public async Task QuitarleTodosLosRoles_RevocaLaSesionViva()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.Update(Id, new SystemUsersController.UpdateSystemUserRequest(
            "staff@casa.com", Role: "", IsActive: true));

        DebeEstarRevocada(usuario, "quedarse sin roles también es un cambio de roles");
    }

    [Fact]
    public async Task DesactivarDesdeElFormulario_RevocaLaSesionViva()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.Update(Id, new SystemUsersController.UpdateSystemUserRequest(
            "staff@casa.com", AppRoles.Admin, IsActive: false));

        DebeEstarRevocada(usuario, "desactivar retira el derecho a entrar");
    }

    [Fact]
    public async Task GuardarSinCambiarNada_NoRevocaNada()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.Update(Id, new SystemUsersController.UpdateSystemUserRequest(
            "staff@casa.com", AppRoles.Admin, IsActive: true));

        usuario.RefreshToken.Should().NotBeNull(
            "un PUT que no cambia nada no es un cambio de postura de seguridad; revocar aquí " +
            "convertiría abrir el formulario y guardar en un cierre de sesión");
    }

    /// <summary>El correo se compara sin distinguir mayúsculas: cambiar la caja no es cambiar de cuenta.</summary>
    [Fact]
    public async Task CambiarSoloLaCajaDelCorreo_NoRevocaNada()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.Update(Id, new SystemUsersController.UpdateSystemUserRequest(
            "Staff@Casa.com", AppRoles.Admin, IsActive: true));

        usuario.RefreshToken.Should().NotBeNull();
    }

    [Fact]
    public async Task ApagarLaCuenta_RevocaLaSesionViva()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.ToggleStatus(Id, new SystemUsersController.ToggleStatusRequest(IsActive: false));

        DebeEstarRevocada(usuario, "apagar retira el derecho a entrar");
    }

    /// <summary>
    /// Reactivar no revoca: no retira ningún derecho, y de todas formas la sesión que hubiera ya
    /// está revocada desde que se apagó.
    /// </summary>
    [Fact]
    public async Task ReactivarLaCuenta_NoRevocaNada()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin], activa: false);

        await control.ToggleStatus(Id, new SystemUsersController.ToggleStatusRequest(IsActive: true));

        usuario.RefreshToken.Should().NotBeNull();
    }

    [Fact]
    public async Task DarDeBaja_RevocaLaSesionViva()
    {
        var (control, usuario, _) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        await control.Deactivate(Id);

        DebeEstarRevocada(usuario,
            "el DELETE de esta superficie es una baja lógica, pero retira el derecho a entrar igual");
    }

    /// <summary>
    /// Revocar en memoria no vale de nada si nadie lo guarda. La prueba comprueba que el
    /// <c>UpdateAsync</c> ocurre DESPUÉS de dejar los campos a nulo.
    /// </summary>
    [Fact]
    public async Task LaRevocacionSePersiste()
    {
        var (control, usuario, userManager) = Escenario(correo: "staff@casa.com", roles: [AppRoles.Admin]);

        string? tokenAlGuardar = "todavía no se guardó";
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                   .Callback<ApplicationUser>(u => tokenAlGuardar = u.RefreshToken)
                   .ReturnsAsync(IdentityResult.Success);

        await control.Deactivate(Id);

        tokenAlGuardar.Should().BeNull(
            "cuando UpdateAsync recibe la entidad, el refresh token ya tiene que estar a nulo");
        usuario.RefreshToken.Should().BeNull();
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    private static void DebeEstarRevocada(ApplicationUser usuario, string porque)
    {
        usuario.RefreshToken.Should().BeNull(porque);
        usuario.RefreshTokenExpiry.Should().BeNull(porque);
    }

    private static (SystemUsersController, ApplicationUser, Mock<UserManager<ApplicationUser>>) Escenario(
        string correo, string[] roles, bool activa = true)
    {
        var usuario = new ApplicationUser
        {
            Id                 = Id,
            Email              = correo,
            UserName           = correo,
            IsActive           = activa,
            RefreshToken       = "refresco-vivo",
            RefreshTokenExpiry = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(Id)).ReturnsAsync(usuario);
        userManager.Setup(m => m.GetRolesAsync(usuario)).ReturnsAsync(roles.ToList());
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                   .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                   .ReturnsAsync(IdentityResult.Success);

        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(d => d.Now).Returns(new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc));

        return (new SystemUsersController(userManager.Object, reloj.Object), usuario, userManager);
    }
}
