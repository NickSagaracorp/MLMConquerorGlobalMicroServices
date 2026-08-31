using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Controllers;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.RemovePlacement;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// EL SUJETO SALE DEL TOKEN, TAMBIÉN EN EL CENTRO DE NEGOCIOS.
/// </summary>
/// <remarks>
/// LA MISMA FAMILIA QUE 4f4beaf, en el servicio que aquel commit no tocó. Cinco rutas llevaban un
/// <c>{memberId}</c> ajeno en la URL o en el cuerpo bajo un <c>[Authorize]</c> pelado, y ningún
/// manejador de las cinco miraba el token:
///
///   • <c>team/dual-tree/node/{id}</c> y <c>team/dual-tree/stats/{id}</c> — la posición y los puntos
///     de pierna de cualquiera.
///   • <c>commissions/car-bonus/ambassadors/{id}/branch</c> — el desglose de la rama de cualquiera:
///     nombres, nivel de membresía, caducidad y puntos.
///   • <c>POST /placement</c> y <c>DELETE /placement/{id}</c> — colocar o SACAR del árbol binario a
///     cualquiera. Los manejadores sí leían <c>IsAdmin</c>, pero solo para relajar la ventana de 30
///     días y el tope de dos oportunidades; nunca para comprobar de quién era el miembro.
///
/// DOS REGLAS PORQUE HAY DOS SUJETOS, no porque haya dos políticas. La de propiedad es la de siempre
/// —<see cref="CallerIdentity.CanActOnMember"/>—; lo que cambia es a quién alcanza "lo tuyo" en cada
/// pantalla: el visualizador baja por tu subárbol binario, el informe del bono del coche baja por tu
/// red de patrocinio, y colocar exige patrocinio DIRECTO porque mueve puntos.
///
/// SE PRUEBA QUE LA OPERACIÓN NO OCURRE, no solo que se contesta 403: cada prueba comprueba también
/// que el mediador no recibió nada. Un 403 con el efecto ya hecho no cierra nada.
/// </remarks>
public class PropiedadEnElCentroDeNegociosTests : IDisposable
{
    private const string Propio      = "AMB-100001";
    private const string Descendiente = "AMB-100002";
    private const string Ajeno       = "AMB-999999";

    private readonly AppDbContext _db;

    public PropiedadEnElCentroDeNegociosTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        // Binario: Propio es la raíz y Descendiente cuelga de él. Ajeno vive en otro árbol.
        _db.DualTeamTree.AddRange(
            new DualTeamEntity { MemberId = Propio,       HierarchyPath = $"/{Propio}/" },
            new DualTeamEntity { MemberId = Descendiente, ParentMemberId = Propio,
                                 Side = TreeSide.Left,    HierarchyPath = $"/{Propio}/{Descendiente}/" },
            new DualTeamEntity { MemberId = Ajeno,        HierarchyPath = $"/{Ajeno}/" });

        // Patrocinio: la misma forma, en el otro árbol.
        _db.GenealogyTree.AddRange(
            new GenealogyEntity { MemberId = Propio,       HierarchyPath = $"/{Propio}/" },
            new GenealogyEntity { MemberId = Descendiente, ParentMemberId = Propio,
                                  HierarchyPath = $"/{Propio}/{Descendiente}/" },
            new GenealogyEntity { MemberId = Ajeno,        HierarchyPath = $"/{Ajeno}/" });

        _db.MemberProfiles.AddRange(
            Perfil(Propio),
            Perfil(Descendiente, patrocinador: Propio),
            Perfil(Ajeno));

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ===============================================================================================
    //  Árbol binario — dual-tree/node y dual-tree/stats
    // ===============================================================================================

    public static TheoryData<string> LasDosDelArbol() => new() { "node", "stats" };

    [Theory]
    [MemberData(nameof(LasDosDelArbol))]
    public async Task Arbol_DeUnMiembroAjeno_SeRechazaYNoSeConsulta(string ruta)
    {
        var mediador = new Mock<IMediator>();

        DebeSerProhibido(await Arbol(mediador, Miembro(Propio), ruta, Ajeno));
        NuncaSeEnvioDelArbol(mediador);
    }

    [Theory]
    [MemberData(nameof(LasDosDelArbol))]
    public async Task Arbol_DelPropio_SeConsulta(string ruta)
    {
        var mediador = MediadorQueAcepta();

        (await Arbol(mediador, Miembro(Propio), ruta, Propio)).Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// El visualizador baja nodo a nodo por el subárbol del que mira. Cerrar esto a "solo tu propio
    /// identificador" devolvería 403 en la pantalla de todo el mundo, y eso también es romperla.
    /// </summary>
    [Theory]
    [MemberData(nameof(LasDosDelArbol))]
    public async Task Arbol_DeUnDescendienteBinario_SeConsulta(string ruta)
    {
        var mediador = MediadorQueAcepta();

        (await Arbol(mediador, Miembro(Propio), ruta, Descendiente)).Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [MemberData(nameof(LasDosDelArbol))]
    public async Task Arbol_ElPersonalPuedeSobreCualquiera(string ruta)
    {
        var mediador = MediadorQueAcepta();

        (await Arbol(mediador, Personal(), ruta, Ajeno)).Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Sin <c>memberId</c> y sin rol de personal no hay nada contra lo que comparar. Falla cerrado:
    /// "vacío == vacío" no puede ser una autorización.
    /// </summary>
    [Theory]
    [MemberData(nameof(LasDosDelArbol))]
    public async Task Arbol_SinIdentidadDeMiembro_SeRechaza(string ruta)
    {
        var mediador = new Mock<IMediator>();

        DebeSerProhibido(await Arbol(mediador, SinNada(), ruta, Propio));
        NuncaSeEnvioDelArbol(mediador);
    }

    // ===============================================================================================
    //  Bono del coche — la rama de un embajador
    // ===============================================================================================

    [Fact]
    public async Task RamaDelBonoDelCoche_DeUnMiembroAjeno_SeRechazaYNoSeConsulta()
    {
        var mediador = new Mock<IMediator>();

        DebeSerProhibido(await Rama(mediador, Miembro(Propio), Ajeno));
        NuncaSeEnvio<CarBonusBranchDto>(mediador);
    }

    [Fact]
    public async Task RamaDelBonoDelCoche_DeUnDescendienteDeLaRed_SeConsulta()
    {
        var mediador = MediadorQueAcepta();

        (await Rama(mediador, Miembro(Propio), Descendiente)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RamaDelBonoDelCoche_ElPersonalPuedeSobreCualquiera()
    {
        var mediador = MediadorQueAcepta();

        (await Rama(mediador, Personal(), Ajeno)).Should().BeOfType<OkObjectResult>();
    }

    // ===============================================================================================
    //  Colocación — la que mueve dinero
    // ===============================================================================================

    [Fact]
    public async Task Colocar_AUnMiembroQueNoPatrocinas_SeRechazaYNoSeEjecuta()
    {
        var mediador = new Mock<IMediator>();
        var control  = Colocacion(mediador, Miembro(Propio));

        var respuesta = await control.PlaceMember(
            new PlaceMemberRequest
            {
                MemberToPlaceId      = Ajeno,
                TargetParentMemberId = Propio,
                Side                 = "Left"
            }, default);

        DebeSerProhibido(respuesta);
        NuncaSeEnvio<PlaceMemberResult>(mediador);
    }

    /// <summary>
    /// El otro sujeto de la misma operación. Patrocinar al colocado no basta: colgarlo debajo de un
    /// desconocido le mueve los puntos de pierna a ESE, que es exactamente el abuso.
    /// </summary>
    [Fact]
    public async Task Colocar_BajoUnNodoFueraDeTuRed_SeRechazaYNoSeEjecuta()
    {
        var mediador = new Mock<IMediator>();
        var control  = Colocacion(mediador, Miembro(Propio));

        var respuesta = await control.PlaceMember(
            new PlaceMemberRequest
            {
                MemberToPlaceId      = Descendiente,
                TargetParentMemberId = Ajeno,
                Side                 = "Left"
            }, default);

        DebeSerProhibido(respuesta);
        NuncaSeEnvio<PlaceMemberResult>(mediador);
    }

    [Fact]
    public async Task Colocar_AUnPatrocinadoDirectoYBajoTuPropioNodo_SeEjecuta()
    {
        var mediador = MediadorQueAcepta();
        var control  = Colocacion(mediador, Miembro(Propio));

        var respuesta = await control.PlaceMember(
            new PlaceMemberRequest
            {
                MemberToPlaceId      = Descendiente,
                TargetParentMemberId = Propio,
                Side                 = "Left"
            }, default);

        respuesta.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Descolocar_AUnMiembroQueNoPatrocinas_SeRechazaYNoSeEjecuta()
    {
        var mediador = new Mock<IMediator>();
        var control  = Colocacion(mediador, Miembro(Propio));

        DebeSerProhibido(await control.RemovePlacement(Ajeno, default));
        NuncaSeEnvio<RemovePlacementResult>(mediador);
    }

    [Fact]
    public async Task Descolocar_AUnPatrocinadoDirecto_SeEjecuta()
    {
        var mediador = MediadorQueAcepta();
        var control  = Colocacion(mediador, Miembro(Propio));

        (await control.RemovePlacement(Descendiente, default)).Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Un patrocinado de tu patrocinado NO es tuyo para colocar: ya tiene a su propio patrocinador.
    /// Es la diferencia entre la regla de la colocación y la de las lecturas del árbol, y por eso
    /// hay una prueba que la fija.
    /// </summary>
    [Fact]
    public async Task Descolocar_AUnNietoDeLaRed_SeRechaza()
    {
        _db.MemberProfiles.Add(Perfil("AMB-100003", patrocinador: Descendiente));
        _db.GenealogyTree.Add(new GenealogyEntity
        {
            MemberId       = "AMB-100003",
            ParentMemberId = Descendiente,
            HierarchyPath  = $"/{Propio}/{Descendiente}/AMB-100003/"
        });
        _db.SaveChanges();

        var mediador = new Mock<IMediator>();
        var control  = Colocacion(mediador, Miembro(Propio));

        DebeSerProhibido(await control.RemovePlacement("AMB-100003", default));
        NuncaSeEnvio<RemovePlacementResult>(mediador);
    }

    [Fact]
    public async Task Colocacion_ElPersonalPuedeSobreCualquiera()
    {
        var mediador = MediadorQueAcepta();
        var control  = Colocacion(mediador, Personal());

        (await control.RemovePlacement(Ajeno, default)).Should().BeOfType<OkObjectResult>();
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    private MemberProfile Perfil(string memberId, string? patrocinador = null) => new()
    {
        MemberId        = memberId,
        FirstName       = "Prueba",
        LastName        = memberId,
        MemberType      = MemberType.Ambassador,
        Status          = MemberAccountStatus.Active,
        EnrollDate      = DateTime.UtcNow.AddDays(-10),
        SponsorMemberId = patrocinador,
        CreationDate    = DateTime.UtcNow,
        LastUpdateDate  = DateTime.UtcNow,
        CreatedBy       = "seed"
    };

    private static ClaimsPrincipal Miembro(string memberId) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-001"),
                new Claim(CallerIdentity.MemberIdClaim, memberId),
                new Claim(ClaimTypes.Role, AppRoles.Ambassador)
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static ClaimsPrincipal Personal() =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "staff-001"),
                new Claim(ClaimTypes.Role, AppRoles.SupportLevel2)
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static ClaimsPrincipal SinNada() =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-002")],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static Mock<IMediator> MediadorQueAcepta()
    {
        var mediador = new Mock<IMediator>();

        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<DualTreeNodeDto>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<DualTreeNodeDto>.Success(new DualTreeNodeDto()));
        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<DualTreeStatsDto>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<DualTreeStatsDto>.Success(new DualTreeStatsDto()));
        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<CarBonusBranchDto>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<CarBonusBranchDto>.Success(new CarBonusBranchDto()));
        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<PlaceMemberResult>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PlaceMemberResult>.Success(
                    new PlaceMemberResult(Descendiente, "Prueba", Propio, "Left", 1)));
        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<RemovePlacementResult>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RemovePlacementResult>.Success(
                    new RemovePlacementResult(Descendiente, "Prueba", 1)));

        return mediador;
    }

    /// <summary>
    /// Genérico a propósito: <c>IMediator.Send</c> tiene dos sobrecargas y la que usan los
    /// controladores es la de <c>IRequest&lt;TResponse&gt;</c>. Verificar la de <c>object</c> pasaría
    /// siempre —nadie la llama— y la prueba no probaría nada.
    /// </summary>
    private static void NuncaSeEnvio<T>(Mock<IMediator> mediador) =>
        mediador.Verify(
            m => m.Send(It.IsAny<IRequest<Result<T>>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no basta con contestar 403: la consulta o el efecto no pueden llegar a ocurrir");

    private static void NuncaSeEnvioDelArbol(Mock<IMediator> mediador)
    {
        NuncaSeEnvio<DualTreeNodeDto>(mediador);
        NuncaSeEnvio<DualTreeStatsDto>(mediador);
    }

    private Task<IActionResult> Arbol(
        Mock<IMediator> mediador, ClaimsPrincipal usuario, string ruta, string memberId)
    {
        var control = new TeamController(
            mediador.Object,
            new Mock<IDualTeamService>().Object,
            new Mock<IEnrollmentTeamService>().Object,
            new Mock<ICurrentUserService>().Object,
            new DownlineGuard(_db))
        {
            ControllerContext = Contexto(usuario)
        };

        return ruta switch
        {
            "node"  => control.GetDualTreeNode(memberId, default),
            "stats" => control.GetDualTreeStats(memberId, default),
            _       => throw new ArgumentOutOfRangeException(nameof(ruta))
        };
    }

    private Task<IActionResult> Rama(
        Mock<IMediator> mediador, ClaimsPrincipal usuario, string memberId)
    {
        var control = new CommissionsController(mediador.Object, new DownlineGuard(_db))
        {
            ControllerContext = Contexto(usuario)
        };

        return control.GetCarBonusBranch(memberId, default);
    }

    private PlacementController Colocacion(Mock<IMediator> mediador, ClaimsPrincipal usuario) =>
        new(mediador.Object, new DownlineGuard(_db)) { ControllerContext = Contexto(usuario) };

    private static ControllerContext Contexto(ClaimsPrincipal usuario) =>
        new() { HttpContext = new DefaultHttpContext { User = usuario } };

    private static void DebeSerProhibido(IActionResult respuesta)
    {
        var objeto = respuesta.Should().BeOfType<ObjectResult>().Subject;

        objeto.StatusCode.Should().Be(StatusCodes.Status403Forbidden,
            "403 y no 404: quien llama está autenticado y la ruta existe, lo que no tiene es " +
            "permiso sobre esa cuenta");

        objeto.Value.Should().BeOfType<ApiResponse<bool>>()
              .Which.ErrorCode.Should().Be("FORBIDDEN");
    }
}
