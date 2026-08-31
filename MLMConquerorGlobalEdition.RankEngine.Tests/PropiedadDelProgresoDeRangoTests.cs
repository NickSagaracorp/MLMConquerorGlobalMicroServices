using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.RankEngine.Controllers;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.RankEngine.Tests;

/// <summary>
/// EL PROGRESO DE RANGO DE UN MIEMBRO ES SUYO.
/// </summary>
/// <remarks>
/// El comentario de la ruta decía «Members can view their own progress; admins can view any
/// member» y no lo comprobaba nadie: el <c>[Authorize]</c> de la clase solo mira que HAYA sesión, el
/// sujeto salía del <c>{memberId}</c> de la URL y <c>GetRankProgressHandler</c> ni siquiera inyecta
/// <c>ICurrentUserService</c>. Cualquier cuenta autenticada leía los puntos personales, los puntos
/// de pierna y los patrocinados cualificados de cualquier miembro cambiando una cadena.
///
/// Aquí no se admite la descendencia, al contrario que en el centro de negocios: esta ruta la llama
/// el portal para la barra de progreso del PROPIO miembro y el panel tiene su propia superficie. La
/// regla es la de siempre, tal cual: <see cref="CallerIdentity.CanActOnMember"/>.
/// </remarks>
public class PropiedadDelProgresoDeRangoTests
{
    private const string Propio = "AMB-100001";
    private const string Ajeno  = "AMB-999999";

    [Fact]
    public async Task Progreso_DeOtroMiembro_SeRechazaYNoSeConsulta()
    {
        var mediador = new Mock<IMediator>();

        var respuesta = await Control(mediador, Miembro(Propio)).GetProgress(Ajeno, default);

        var objeto = respuesta.Should().BeOfType<ObjectResult>().Subject;
        objeto.StatusCode.Should().Be(StatusCodes.Status403Forbidden,
            "403 y no 404: quien llama está autenticado y la ruta existe, lo que no tiene es " +
            "permiso sobre esa cuenta");
        objeto.Value.Should().BeOfType<ApiResponse<object>>()
              .Which.ErrorCode.Should().Be("FORBIDDEN");

        NuncaSeConsulto(mediador);
    }

    [Fact]
    public async Task Progreso_DelPropio_SeConsulta()
    {
        var mediador = MediadorQueAcepta();

        (await Control(mediador, Miembro(Propio)).GetProgress(Propio, default))
            .Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Progreso_ElPersonalPuedeSobreCualquiera()
    {
        var mediador = MediadorQueAcepta();

        (await Control(mediador, Personal()).GetProgress(Ajeno, default))
            .Should().BeOfType<OkObjectResult>();
    }

    /// <summary>Falla cerrado: sin identidad de miembro no hay nada contra lo que comparar.</summary>
    [Fact]
    public async Task Progreso_SinIdentidadDeMiembro_SeRechaza()
    {
        var mediador = new Mock<IMediator>();

        var respuesta = await Control(mediador, SinNada()).GetProgress(Propio, default);

        respuesta.Should().BeOfType<ObjectResult>()
                 .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        NuncaSeConsulto(mediador);
    }

    private static void NuncaSeConsulto(Mock<IMediator> mediador) =>
        mediador.Verify(
            m => m.Send(It.IsAny<IRequest<Result<RankProgressResponse>>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "no basta con contestar 403: la consulta no puede llegar a ocurrir");

    private static Mock<IMediator> MediadorQueAcepta()
    {
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(It.IsAny<IRequest<Result<RankProgressResponse>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<RankProgressResponse>.Success(new RankProgressResponse()));
        return mediador;
    }

    private static RanksController Control(Mock<IMediator> mediador, ClaimsPrincipal usuario) =>
        new(mediador.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = usuario }
            }
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
}
