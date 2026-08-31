using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StartImpersonation;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Middleware;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests;

/// <summary>
/// EL "SOLO LECTURA" DE UNA SUPLANTACIÓN TIENE QUE VIVIR EN EL TOKEN Y APLICARSE EN EL SERVIDOR.
/// </summary>
/// <remarks>
/// LO QUE HABÍA. <c>StartImpersonationHandler</c> calculaba <c>isReadOnly</c> para el
/// <c>SupportManager</c> sin <c>Admin</c> ni <c>SuperAdmin</c> y lo devolvía en el CUERPO de la
/// respuesta, como un dato informativo. Al token no llegaba nada: dos horas con los roles completos
/// del miembro suplantado. Quien usara ese token contra la API directamente no estaba limitado por
/// nada, y "la interfaz lo pinta en modo consulta" no es una autorización.
///
/// LO QUE SE PRUEBA AQUÍ, en tres capas, porque el agujero solo se cierra si están las tres:
///
///   1. QUE LA RESTRICCIÓN ENTRA EN EL TOKEN — el manejador se la pasa al emisor.
///   2. QUE EL SERVIDOR LA APLICA — el middleware rechaza cualquier método de escritura, y las
///      pruebas comprueban además que la petición NO LLEGA a la ruta: un 403 que igualmente ejecuta
///      el efecto sería decorativo.
///   3. QUE ESTÁ PUESTA EN LOS SIETE SERVICIOS que aceptan estos tokens. Falta en uno = ese
///      servicio vuelve a aceptar escrituras de una sesión de solo lectura.
/// </remarks>
public class SuplantacionDeSoloLecturaTests
{
    private static readonly DateTime Ahora = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    // ===============================================================================================
    //  1. La restricción entra en el token
    // ===============================================================================================

    [Fact]
    public async Task SupportManagerSolo_ElTokenSeEmiteRestringidoALectura()
    {
        var (db, userManager, jwt) = Escenario(rolesDelMiembro: [AppRoles.Ambassador]);

        var resultado = await Manejador(db, userManager, jwt).Handle(
            new StartImpersonationCommand("admin-001", [AppRoles.SupportManager], "AMB-001"),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.IsReadOnly.Should().BeTrue();

        jwt.Verify(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                true,               // isImpersonating
                "admin-001",        // impersonatedBy
                It.IsAny<string?>(),
                true),              // impersonationReadOnly — ESTO es lo que faltaba
            Times.Once,
            "la restricción tiene que viajar firmada dentro del token, no solo en el cuerpo de la " +
            "respuesta que la interfaz puede ignorar");
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task ConAdminOSuperAdmin_ElTokenNoSeRestringe(string rolExtra)
    {
        var (db, userManager, jwt) = Escenario(rolesDelMiembro: [AppRoles.Ambassador]);

        var resultado = await Manejador(db, userManager, jwt).Handle(
            new StartImpersonationCommand("admin-001", [AppRoles.SupportManager, rolExtra], "AMB-001"),
            CancellationToken.None);

        resultado.Value!.IsReadOnly.Should().BeFalse();

        jwt.Verify(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(),
                false),
            Times.Once);
    }

    /// <summary>
    /// El token de suplantación se emite con los roles DEL SUPLANTADO. Si el suplantado tuviera un
    /// rol de panel, suplantarlo sería subir de privilegio: un SupportManager entraría y saldría con
    /// los roles de esa cuenta. La superficie existe para atender a miembros.
    /// </summary>
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.BillingManager)]
    [InlineData(AppRoles.IT)]
    public async Task NoSeSuplantaAUnaCuentaConRolDePersonal(string rolDePersonal)
    {
        var (db, userManager, jwt) = Escenario(rolesDelMiembro: [AppRoles.Ambassador, rolDePersonal]);

        var resultado = await Manejador(db, userManager, jwt).Handle(
            new StartImpersonationCommand("admin-001", [AppRoles.SupportManager], "AMB-001"),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeFalse();
        resultado.ErrorCode.Should().Be("TARGET_IS_STAFF");

        jwt.Verify(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()),
            Times.Never,
            "un rechazo que igualmente emite el token es un rechazo decorativo");
    }

    // ===============================================================================================
    //  2. El servidor la aplica
    // ===============================================================================================

    public static TheoryData<string> MetodosQueEscriben() =>
        new() { "POST", "PUT", "PATCH", "DELETE" };

    [Theory]
    [MemberData(nameof(MetodosQueEscriben))]
    public async Task TokenDeSoloLectura_NoAlcanzaNingunMetodoDeEscritura(string metodo)
    {
        var (contexto, llego) = Peticion(metodo, DeSoloLectura());

        await Middleware(llego).InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        llego.Value.Should().BeFalse(
            "no basta con contestar 403: la ruta no puede llegar a ejecutarse, o el efecto ya está " +
            "hecho cuando el 403 sale por el cable");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task TokenDeSoloLectura_SigueLeyendo(string metodo)
    {
        var (contexto, llego) = Peticion(metodo, DeSoloLectura());

        await Middleware(llego).InvokeAsync(contexto);

        llego.Value.Should().BeTrue("solo lectura es solo lectura, no es un bloqueo total");
    }

    /// <summary>
    /// Las rejillas del equipo son lecturas que llegan por POST porque el filtro va en el cuerpo.
    /// Sin la excepción explícita, soporte no podría ver el equipo del miembro que está atendiendo.
    /// </summary>
    [Fact]
    public async Task TokenDeSoloLectura_AlcanzaUnPostMarcadoComoLectura()
    {
        var (contexto, llego) = Peticion("POST", DeSoloLectura(), new ReadOnlySafeAttribute());

        await Middleware(llego).InvokeAsync(contexto);

        llego.Value.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(MetodosQueEscriben))]
    public async Task UnTokenNormal_NoSeVeAfectado(string metodo)
    {
        var (contexto, llego) = Peticion(metodo, Miembro());

        await Middleware(llego).InvokeAsync(contexto);

        llego.Value.Should().BeTrue(
            "el claim solo lo llevan los tokens de suplantación restringidos; el resto del tráfico " +
            "no cambia de comportamiento");
    }

    /// <summary>
    /// Una suplantación de SuperAdmin o Admin no lleva el claim, así que escribe. Es la otra mitad
    /// de la regla y hay que probarla: si el middleware bloqueara toda suplantación, habríamos roto
    /// la suplantación completa en vez de limitar la restringida.
    /// </summary>
    [Fact]
    public async Task UnaSuplantacionSinRestriccion_SigueEscribiendo()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-001"),
                new Claim("impersonating", "true"),
                new Claim("impersonatedBy", "admin-001")
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

        var (contexto, llego) = Peticion("POST", principal);

        await Middleware(llego).InvokeAsync(contexto);

        llego.Value.Should().BeTrue();
    }

    /// <summary>
    /// El valor se compara exacto. Un token que dijera <c>"false"</c> —o cualquier otra cosa— no
    /// está restringido; solo <c>"true"</c> restringe.
    /// </summary>
    [Theory]
    [InlineData("false")]
    [InlineData("")]
    [InlineData("no")]
    public async Task ElClaimSoloRestringeConElValorExacto(string valor)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ImpersonationScope.ReadOnlyClaim, valor)],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

        var (contexto, llego) = Peticion("POST", principal);

        await Middleware(llego).InvokeAsync(contexto);

        llego.Value.Should().BeTrue();
    }

    // ===============================================================================================
    //  3. Está puesta en los siete servicios
    // ===============================================================================================

    public static TheoryData<string> LosSieteQueAceptanEstosTokens() => new()
    {
        "MLMConquerorGlobalEdition.AdminAPI",
        "MLMConquerorGlobalEdition.SignupAPI",
        "MLMConquerorGlobalEdition.BizCenter",
        "MLMConquerorGlobalEdition.RankEngine",
        "MLMConquerorGlobalEdition.TicketManagementSystem",
        "MLMConquerorGlobalEdition.Billing",
        "MLMConquerorGlobalEdition.CommissionEngine"
    };

    [Theory]
    [MemberData(nameof(LosSieteQueAceptanEstosTokens))]
    public void CadaServicioRegistraElGuardaDeSoloLectura(string servicio)
    {
        var programa = Path.Combine(RaizDelRepositorio(), servicio, "Program.cs");

        File.Exists(programa).Should().BeTrue($"no se encontró {programa}");

        var fuente = File.ReadAllText(programa);

        fuente.Should().Contain("UseImpersonationReadOnly()",
            $"{servicio} acepta tokens emitidos por AdminAPI. Sin este middleware, una sesión de " +
            "suplantación declarada de solo lectura puede escribir contra este servicio, y el " +
            "'solo lectura' vuelve a ser un adorno de la interfaz.");

        // Detrás de la autorización: un 401 o un 403 por rol se contestan antes que este.
        fuente.IndexOf("UseImpersonationReadOnly()", StringComparison.Ordinal)
              .Should().BeGreaterThan(
                  fuente.IndexOf("UseAuthorization()", StringComparison.Ordinal),
                  $"en {servicio} el guarda tiene que ir después de UseAuthorization()");
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    private static ClaimsPrincipal DeSoloLectura() =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-001"),
                new Claim(CallerIdentity.MemberIdClaim, "AMB-001"),
                new Claim("impersonating", "true"),
                new Claim("impersonatedBy", "admin-001"),
                new Claim(ImpersonationScope.ReadOnlyClaim, ImpersonationScope.ReadOnlyValue),
                new Claim(ClaimTypes.Role, AppRoles.Ambassador)
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static ClaimsPrincipal Miembro() =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-002"),
                new Claim(CallerIdentity.MemberIdClaim, "AMB-002"),
                new Claim(ClaimTypes.Role, AppRoles.Ambassador)
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    /// <summary>
    /// Un contexto con su endpoint resuelto —el middleware lee metadatos, así que sin endpoint la
    /// prueba no probaría lo mismo que ocurre en ejecución— y un testigo de si la ruta se ejecutó.
    /// </summary>
    private static (HttpContext, Testigo) Peticion(
        string metodo, ClaimsPrincipal usuario, params object[] metadatos)
    {
        var contexto = new DefaultHttpContext
        {
            User = usuario,
            Response = { Body = new MemoryStream() }
        };
        contexto.Request.Method = metodo;
        contexto.Request.Path   = "/api/v1/prueba";

        contexto.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(
            _ => Task.CompletedTask,
            new Microsoft.AspNetCore.Http.EndpointMetadataCollection(metadatos),
            "prueba"));

        return (contexto, new Testigo());
    }

    private static ImpersonationReadOnlyMiddleware Middleware(Testigo llego) =>
        new(_ => { llego.Value = true; return Task.CompletedTask; },
            NullLogger<ImpersonationReadOnlyMiddleware>.Instance);

    private sealed class Testigo { public bool Value { get; set; } }

    private static (Repository.Context.AppDbContext, Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>, Mock<IJwtService>)
        Escenario(string[] rolesDelMiembro)
    {
        var db = InMemoryDbHelper.Create();

        var member = new MemberProfile
        {
            MemberId       = "AMB-001",
            FirstName      = "Alice",
            LastName       = "Doe",
            Country        = "US",
            Status         = MemberAccountStatus.Active,
            MemberType     = MemberType.Ambassador,
            EnrollDate     = Ahora.AddDays(-30),
            CreationDate   = Ahora.AddDays(-30),
            LastUpdateDate = Ahora,
            CreatedBy      = "seed"
        };
        db.MemberProfiles.Add(member);
        db.SaveChanges();

        var targetUser = new ApplicationUser
        {
            Id              = "user-001",
            Email           = "amb@test.com",
            MemberProfileId = member.MemberId,
            IsActive        = true
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.Users).Returns(new List<ApplicationUser> { targetUser }.AsAsyncQueryable());
        userManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(rolesDelMiembro.ToList());

        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns("token-de-suplantacion");

        return (db, userManager, jwt);
    }

    private static StartImpersonationHandler Manejador(
        Repository.Context.AppDbContext db,
        Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> userManager,
        Mock<IJwtService> jwt)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(d => d.Now).Returns(Ahora);

        return new StartImpersonationHandler(
            db, userManager.Object, jwt.Object, reloj.Object,
            NullLogger<StartImpersonationHandler>.Instance);
    }

    /// <summary>Sube desde la salida de las pruebas hasta la carpeta del archivo de solucion.</summary>
    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null &&
               !File.Exists(Path.Combine(directorio.FullName, "MLMConquerorGlobalEdition.slnx")))
        {
            directorio = directorio.Parent;
        }

        directorio.Should().NotBeNull("la prueba tiene que poder localizar la raíz del repositorio");
        return directorio!.FullName;
    }
}
