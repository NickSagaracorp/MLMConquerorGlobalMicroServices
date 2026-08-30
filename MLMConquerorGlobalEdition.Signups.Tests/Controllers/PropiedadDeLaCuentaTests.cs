using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Controllers;

/// <summary>
/// EL SUJETO SALE DEL TOKEN, NO DE LA URL.
///
/// Las seis rutas de <c>MembershipController</c> y <c>PlacementController</c> llevan un
/// <c>[Authorize]</c> pelado —comprueba que HAY sesión, nunca de quién es— y tomaban el
/// <c>{memberId}</c> del sujeto de la RUTA. Ningún manejador de los seis miraba el token: ni uno
/// inyecta <c>ICurrentUserService</c>. Con eso, cualquier cuenta autenticada podía subir, bajar o
/// cancelar la membresía de otro miembro, y colocarlo o SACARLO del árbol binario, cambiando una
/// cadena en la URL.
/// </summary>
/// <remarks>
/// LO QUE SE PRUEBA AQUÍ, y no es lo mismo que "devuelve 403": que la operación NO LLEGA A OCURRIR.
/// Un rechazo que igualmente manda el comando al mediador sería un 403 decorativo con el efecto ya
/// hecho, así que cada prueba comprueba también que el mediador no recibió nada.
///
/// POR QUÉ EL PERSONAL PASA. La superficie administrativa paralela
/// —<c>/api/v1/admin/members/{memberId}/membership</c>— ya existe con su lista de roles, y es
/// justamente lo que demuestra que ESTAS rutas son las de autoservicio. Dejar al personal fuera
/// tampoco sería más seguro: entraría por la otra puerta.
/// </remarks>
public class PropiedadDeLaCuentaTests
{
    private const string Propio = "AMB-100001";
    private const string Ajeno  = "AMB-999999";

    // ===============================================================================================
    //  Membresía
    // ===============================================================================================

    public static TheoryData<string> LasTresDeMembresia() =>
        new() { "upgrade", "downgrade", "cancel" };

    [Theory]
    [MemberData(nameof(LasTresDeMembresia))]
    public async Task Membresia_SobreLaCuentaDeOtro_SeRechazaYNoSeEjecuta(string operacion)
    {
        var mediador   = new Mock<IMediator>();
        var controlador = Membresia(mediador, Miembro(Propio));

        var respuesta = await EjecutarMembresia(controlador, operacion, Ajeno);

        DebeSerProhibido(respuesta);
        mediador.Verify(m => m.Send(It.IsAny<IRequest<Result<bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(LasTresDeMembresia))]
    public async Task Membresia_SobreLaPropia_SeEjecuta(string operacion)
    {
        var mediador    = MediadorQueAcepta();
        var controlador = Membresia(mediador, Miembro(Propio));

        var respuesta = await EjecutarMembresia(controlador, operacion, Propio);

        respuesta.Should().BeOfType<OkObjectResult>();
        mediador.Verify(m => m.Send(It.IsAny<IRequest<Result<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(LasTresDeMembresia))]
    public async Task Membresia_ElPersonalPuedeSobreCualquiera(string operacion)
    {
        var mediador    = MediadorQueAcepta();
        var controlador = Membresia(mediador, Personal());

        var respuesta = await EjecutarMembresia(controlador, operacion, Ajeno);

        respuesta.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Un token sin <c>memberId</c> y sin rol de personal —una cuenta a medias, o un token de otra
    /// familia— no puede actuar sobre nadie. Falla cerrado: sin identidad de miembro no hay nada
    /// contra lo que comparar, y "vacío == vacío" no puede ser una autorización.
    /// </summary>
    [Fact]
    public async Task Membresia_SinIdentidadDeMiembro_SeRechaza()
    {
        var mediador    = new Mock<IMediator>();
        var controlador = Membresia(mediador, SinNada());

        DebeSerProhibido(await EjecutarMembresia(controlador, "cancel", Propio));
        mediador.Verify(m => m.Send(It.IsAny<IRequest<Result<bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===============================================================================================
    //  Colocación en el árbol
    // ===============================================================================================

    public static TheoryData<string> LasTresDeColocacion() =>
        new() { "place", "unplace", "validate" };

    [Theory]
    [MemberData(nameof(LasTresDeColocacion))]
    public async Task Colocacion_SobreLaCuentaDeOtro_SeRechazaYNoSeEjecuta(string operacion)
    {
        var mediador    = new Mock<IMediator>();
        var controlador = Colocacion(mediador, Miembro(Propio));

        var respuesta = await EjecutarColocacion(controlador, operacion, Ajeno);

        DebeSerProhibido(respuesta);
        mediador.Verify(m => m.Send(It.IsAny<IRequest<Result<bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(LasTresDeColocacion))]
    public async Task Colocacion_SobreLaPropia_SeEjecuta(string operacion)
    {
        var mediador    = MediadorQueAcepta();
        var controlador = Colocacion(mediador, Miembro(Propio));

        var respuesta = await EjecutarColocacion(controlador, operacion, Propio);

        respuesta.Should().BeOfType<OkObjectResult>();
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

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
        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<bool>.Success(true));
        return mediador;
    }

    private static MembershipController Membresia(Mock<IMediator> mediador, ClaimsPrincipal usuario) =>
        new(mediador.Object) { ControllerContext = Contexto(usuario) };

    private static PlacementController Colocacion(Mock<IMediator> mediador, ClaimsPrincipal usuario) =>
        new(mediador.Object) { ControllerContext = Contexto(usuario) };

    private static ControllerContext Contexto(ClaimsPrincipal usuario) =>
        new() { HttpContext = new DefaultHttpContext { User = usuario } };

    private static Task<IActionResult> EjecutarMembresia(
        MembershipController controlador, string operacion, string memberId) => operacion switch
        {
            "upgrade"   => controlador.Upgrade(memberId, new MembershipChangeRequest(), default),
            "downgrade" => controlador.Downgrade(memberId, new MembershipChangeRequest(), default),
            "cancel"    => controlador.Cancel(memberId, default),
            _           => throw new ArgumentOutOfRangeException(nameof(operacion))
        };

    private static Task<IActionResult> EjecutarColocacion(
        PlacementController controlador, string operacion, string memberId) => operacion switch
        {
            "place"    => controlador.Place(memberId, new PlacementRequest(), default),
            "unplace"  => controlador.Unplace(memberId, default),
            "validate" => controlador.Validate(memberId, new PlacementRequest(), default),
            _          => throw new ArgumentOutOfRangeException(nameof(operacion))
        };

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
