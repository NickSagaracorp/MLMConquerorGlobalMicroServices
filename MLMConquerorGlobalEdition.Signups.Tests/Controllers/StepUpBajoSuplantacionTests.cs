using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.StepUp;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Controllers;

/// <summary>
/// EL STEP-UP NO SE PIDE DESDE UNA SESIÓN SUPLANTADA, NI SIQUIERA LA COMPLETA.
///
/// El middleware de solo lectura corta las escrituras de la suplantación restringida, pero el
/// suplantador COMPLETO (Admin/SuperAdmin) no está restringido y llegaría al reto: el código saldría
/// al correo, teléfono o autenticador del miembro. Cada prueba comprueba también que el mediador no
/// recibió nada, porque un 403 con el código ya enviado sería decorativo.
/// </summary>
public class StepUpBajoSuplantacionTests
{
    private const string Miembro = "user-001";

    private static ClaimsPrincipal Usuario(bool suplantado, bool soloLectura = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Miembro) };
        if (suplantado) claims.Add(new Claim(ImpersonationScope.ImpersonatedByClaim, "admin-9"));
        if (soloLectura) claims.Add(new Claim(ImpersonationScope.ReadOnlyClaim, ImpersonationScope.ReadOnlyValue));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    private static AuthController Controlador(Mock<IMediator> mediador, ClaimsPrincipal usuario) =>
        new(mediador.Object, new Mock<IJwtService>().Object)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = usuario } }
        };

    private static Mock<IMediator> Mediador()
    {
        var m = new Mock<IMediator>();
        m.Setup(x => x.Send(It.IsAny<IssueStepUpCommand>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<StepUpChallengeResponse>.Success(new StepUpChallengeResponse()));
        m.Setup(x => x.Send(It.IsAny<VerifyStepUpCommand>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync(Result<StepUpGrantResponse>.Success(new StepUpGrantResponse()));
        return m;
    }

    private static void DebeSerProhibido<T>(IActionResult respuesta)
    {
        var objeto = respuesta.Should().BeOfType<ObjectResult>().Subject;
        objeto.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        objeto.Value.Should().BeOfType<ApiResponse<T>>()
              .Which.ErrorCode.Should().Be(StepUpGrant.ImpersonationNotAllowedErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reto_con_token_de_suplantacion_se_rechaza_y_no_envia_codigo(bool soloLectura)
    {
        var mediador = Mediador();
        var c = Controlador(mediador, Usuario(suplantado: true, soloLectura));

        var r = await c.StepUpChallenge(new StepUpChallengeRequest { OperationKey = StepUpOperations.PayoutMethodChange }, default);

        DebeSerProhibido<StepUpChallengeResponse>(r);
        mediador.Verify(m => m.Send(It.IsAny<IssueStepUpCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Canje_con_token_de_suplantacion_se_rechaza_y_no_toca_el_codigo(bool soloLectura)
    {
        var mediador = Mediador();
        var c = Controlador(mediador, Usuario(suplantado: true, soloLectura));

        var r = await c.StepUpVerify(new StepUpVerifyRequest { OperationKey = StepUpOperations.PayoutMethodChange }, default);

        DebeSerProhibido<StepUpGrantResponse>(r);
        mediador.Verify(m => m.Send(It.IsAny<VerifyStepUpCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_suplantacion_el_reto_y_el_canje_siguen_su_camino()
    {
        var mediador = Mediador();
        var c = Controlador(mediador, Usuario(suplantado: false));

        (await c.StepUpChallenge(new StepUpChallengeRequest { OperationKey = StepUpOperations.PayoutMethodChange }, default))
            .Should().BeOfType<OkObjectResult>();
        (await c.StepUpVerify(new StepUpVerifyRequest { OperationKey = StepUpOperations.PayoutMethodChange }, default))
            .Should().BeOfType<OkObjectResult>();

        mediador.Verify(m => m.Send(It.IsAny<IssueStepUpCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        mediador.Verify(m => m.Send(It.IsAny<VerifyStepUpCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
