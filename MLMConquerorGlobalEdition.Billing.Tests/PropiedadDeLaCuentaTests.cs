using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MLMConquerorGlobalEdition.Billing.Controllers;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.Charge;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.Billing.Tests;

/// <summary>
/// EL OBJETIVO DE UN COBRO SALE DEL TOKEN, NO DEL CUERPO.
///
/// <c>BillingController</c> llevaba un <c>[Authorize]</c> de clase sin roles y tomaba el
/// <c>MemberId</c> del objetivo del CUERPO de la petición. <c>ICurrentUserService</c> se usaba solo
/// para rellenar <c>CreatedBy</c>/<c>LastUpdateBy</c> —auditoría—, nunca para decidir. Con eso,
/// cualquier cuenta autenticada podía cobrarle la tarjeta a otro miembro, renovarle la membresía o
/// disparar el pago de sus comisiones escribiendo su identificador en el JSON.
/// </summary>
/// <remarks>
/// SON DOS REGLAS Y NO UNA, y la diferencia está en si la operación tiene un sujeto propio:
///
///   • <c>charge</c> y <c>memberships/renew</c> lo tienen —la cuenta a la que se le cobra—, así que
///     valen para autoservicio con la regla del resto del sistema: o es tuya, o eres personal.
///
///   • <c>refund</c> y <c>wallets/payout</c> no lo tienen: una devolución identifica un pago y un
///     pago de comisiones mueve dinero de la casa. Van con los tres roles con los que ya está
///     cerrada la superficie administrativa de facturación, y eso se comprueba sobre el ATRIBUTO —
///     una comprobación dentro del método no serviría, porque quien decide un
///     <c>[Authorize(Roles=…)]</c> es la tubería y no el código del controlador.
/// </remarks>
public class PropiedadDeLaCuentaTests
{
    private const string Propio = "AMB-100001";
    private const string Ajeno  = "AMB-999999";

    // ===============================================================================================
    //  Lo que tiene sujeto propio: o es tuyo, o eres personal
    // ===============================================================================================

    [Fact]
    public async Task Cobrar_ACuentaAjena_SeRechazaYNoSeEjecuta()
    {
        var mediador    = new Mock<IMediator>();
        var controlador = Controlador(mediador, Miembro(Propio));

        var respuesta = await controlador.Charge(new ChargeRequest { MemberId = Ajeno }, default);

        DebeSerProhibido(respuesta);
        NoSeMandoNada(mediador);
    }

    [Fact]
    public async Task Cobrar_ALaPropia_SeEjecuta()
    {
        var mediador    = MediadorQueCobra();
        var controlador = Controlador(mediador, Miembro(Propio));

        var respuesta = await controlador.Charge(new ChargeRequest { MemberId = Propio }, default);

        respuesta.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cobrar_ElPersonalPuedeSobreCualquiera()
    {
        var mediador    = MediadorQueCobra();
        var controlador = Controlador(mediador, Personal(AppRoles.BillingManager));

        var respuesta = await controlador.Charge(new ChargeRequest { MemberId = Ajeno }, default);

        respuesta.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Renovar_LaMembresiaDeOtro_SeRechazaYNoSeEjecuta()
    {
        var mediador    = new Mock<IMediator>();
        var controlador = Controlador(mediador, Miembro(Propio));

        var respuesta = await controlador.RenewMembership(
            new MembershipRenewalRequest { MemberId = Ajeno }, default);

        DebeSerProhibido(respuesta);
        NoSeMandoNada(mediador);
    }

    /// <summary>
    /// Un cuerpo sin <c>MemberId</c> tampoco pasa. Falla cerrado: sin objetivo no hay nada contra lo
    /// que comparar, y una cadena vacía que coincida con otra cadena vacía no es una autorización.
    /// </summary>
    [Fact]
    public async Task Cobrar_SinObjetivoEnElCuerpo_SeRechaza()
    {
        var mediador    = new Mock<IMediator>();
        var controlador = Controlador(mediador, Miembro(Propio));

        DebeSerProhibido(await controlador.Charge(new ChargeRequest { MemberId = string.Empty }, default));
        NoSeMandoNada(mediador);
    }

    // ===============================================================================================
    //  Lo que no tiene sujeto propio: tesorería
    // ===============================================================================================

    public static TheoryData<string> LasDosDeTesoreria() => new() { "Refund", "Payout" };

    [Theory]
    [MemberData(nameof(LasDosDeTesoreria))]
    public void Tesoreria_ExigeLosRolesDeFacturacion(string metodo)
    {
        var atributo = typeof(BillingController)
            .GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<AuthorizeAttribute>();

        atributo.Should().NotBeNull(
            "mover dinero que no es de una sola cuenta no puede quedarse con el [Authorize] pelado " +
            "de la clase, que solo comprueba que hay sesión");

        var roles = atributo!.Roles!.Split(',');

        roles.Should().BeEquivalentTo(
            [AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.BillingManager],
            "es la misma lista con la que ya está cerrada la superficie administrativa de " +
            "facturación; una lista propia aquí sería una política nueva inventada en un sitio");
    }

    /// <summary>
    /// Y las dos de autoservicio NO llevan roles: cerrarlas por rol dejaría fuera al miembro que
    /// paga lo suyo, que es justo para quien existen.
    /// </summary>
    [Theory]
    [InlineData("Charge")]
    [InlineData("RenewMembership")]
    public void Autoservicio_NoSeCierraPorRol(string metodo)
    {
        typeof(BillingController)
            .GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttribute<AuthorizeAttribute>()
            .Should().BeNull();
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

    private static ClaimsPrincipal Personal(string rol) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "staff-001"),
                new Claim(ClaimTypes.Role, rol)
            ],
            "prueba", ClaimTypes.NameIdentifier, ClaimTypes.Role));

    private static Mock<IMediator> MediadorQueCobra()
    {
        var mediador = new Mock<IMediator>();
        mediador.Setup(m => m.Send(
                    It.IsAny<IRequest<Result<ChargeResponse>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChargeResponse>.Success(new ChargeResponse()));
        return mediador;
    }

    private static BillingController Controlador(Mock<IMediator> mediador, ClaimsPrincipal usuario) =>
        new(mediador.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = usuario }
            }
        };

    private static void NoSeMandoNada(Mock<IMediator> mediador) =>
        mediador.Verify(m => m.Send(
            It.IsAny<IRequest<Result<ChargeResponse>>>(), It.IsAny<CancellationToken>()), Times.Never);

    private static void DebeSerProhibido(IActionResult respuesta)
    {
        var objeto = respuesta.Should().BeOfType<ObjectResult>().Subject;

        objeto.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        objeto.Value.Should().BeOfType<ApiResponse<bool>>()
              .Which.ErrorCode.Should().Be("FORBIDDEN");
    }
}
