using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.ConfirmCryptoPayment;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// Quién puede confirmar un cobro en cripto, comprobado EN EL SERVIDOR.
///
/// El dueño del producto fijó cuatro roles: Admin, SuperAdmin, SupportLevel3 y BillingManager.
/// Esconder el botón en AdminWeb no es control de acceso; lo que tiene que rebotar es la llamada
/// directa a la API. Esta prueba mira el atributo que hace rebotar esa llamada.
///
/// POR QUÉ POR REFLEXIÓN Y NO LEVANTANDO UN SERVIDOR: en toda la solución no hay ni un
/// WebApplicationFactory —ni AdminAPI.Tests ni Signups.Tests alojan la tubería—, así que una
/// prueba de integración HTTP aquí sería la única de su especie y arrastraría
/// Microsoft.AspNetCore.Mvc.Testing a un proyecto que hoy no lo tiene. Lo que sí se puede
/// comprobar sin nada de eso es que el atributo existe, que lleva exactamente esos cuatro roles
/// y que sale de la constante compartida y no de una cadena escrita a mano que se pueda quedar
/// atrás. El rebote de verdad se verificó en caliente contra la API levantada.
/// </summary>
public class ConfirmarCobroEnCriptoRolesTests
{
    private static readonly string[] LosCuatro =
        [AppRoles.Admin, AppRoles.SuperAdmin, AppRoles.SupportLevel3, AppRoles.BillingManager];

    private static MethodInfo EndpointDeConfirmacion =>
        typeof(SignupsController).GetMethod(nameof(SignupsController.ConfirmCryptoPayment))!;

    [Fact]
    public void ElEndpointDeConfirmacion_ExigeAutenticacionYRol()
    {
        var attr = EndpointDeConfirmacion.GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull(
            "sin [Authorize] cualquiera con la URL activaría una membresía y dispararía comisiones.");
        attr!.Roles.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ElEndpointDeConfirmacion_AdmiteExactamenteLosCuatroRolesElegidos()
    {
        var roles = EndpointDeConfirmacion
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        roles.Should().BeEquivalentTo(LosCuatro);
    }

    [Theory]
    [InlineData(AppRoles.CommissionManager)]
    [InlineData(AppRoles.SupportManager)]
    [InlineData(AppRoles.SupportLevel1)]
    [InlineData(AppRoles.SupportLevel2)]
    [InlineData(AppRoles.IT)]
    [InlineData(AppRoles.Ambassador)]
    [InlineData(AppRoles.Member)]
    public void ElEndpointDeConfirmacion_RechazaAlResto(string rolNoAutorizado)
    {
        var roles = EndpointDeConfirmacion
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Roles!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        roles.Should().NotContain(rolNoAutorizado);
    }

    [Fact]
    public void LaListaDeAprobadores_EstaEscritaUnaSolaVez()
    {
        // Si alguien cambia la constante compartida, el atributo del controlador cambia con ella.
        // Si alguien escribe la lista a mano en el controlador, esta prueba se pone roja.
        EndpointDeConfirmacion.GetCustomAttribute<AuthorizeAttribute>()!.Roles
            .Should().Be(AppRoles.CryptoPaymentApprovers);

        AppRoles.CryptoPaymentApproverRoles.Should().BeEquivalentTo(LosCuatro);
    }

    [Fact]
    public void CompletarElAlta_SigueSiendoAnonimo()
    {
        // El asistente de alta lo usa gente que todavía no tiene cuenta: si esto llevara
        // [Authorize] no se podría dar de alta nadie.
        typeof(SignupsController)
            .GetMethod(nameof(SignupsController.CompleteSignup))!
            .GetCustomAttribute<AuthorizeAttribute>()
            .Should().BeNull();

        typeof(SignupsController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().BeNull("el controlador de altas es público; solo la confirmación está cerrada.");
    }

    // ── El validador de la confirmación ─────────────────────────────────────────

    [Fact]
    public void Confirmar_SinIdentificadorDeTransaccion_NoValida()
    {
        var v = new ConfirmCryptoPaymentValidator();

        var r = v.Validate(new ConfirmCryptoPaymentCommand(
            "ORD-1", new ConfirmCryptoPaymentRequest { CryptoTransactionId = string.Empty },
            "usr-1", "admin@example.com"));

        r.IsValid.Should().BeFalse(
            "al confirmar sí hace falta el hash: es el único momento en que existe, y sin él no hay cotejo posible.");
    }

    [Fact]
    public void Confirmar_ConIdentificadorConBasura_NoValida()
    {
        var v = new ConfirmCryptoPaymentValidator();

        var r = v.Validate(new ConfirmCryptoPaymentCommand(
            "ORD-1", new ConfirmCryptoPaymentRequest { CryptoTransactionId = "tx_<script>" },
            "usr-1", "admin@example.com"));

        r.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Confirmar_SinSaberQuienAprueba_NoValida()
    {
        var v = new ConfirmCryptoPaymentValidator();

        var r = v.Validate(new ConfirmCryptoPaymentCommand(
            "ORD-1", new ConfirmCryptoPaymentRequest { CryptoTransactionId = "abc123" },
            string.Empty, string.Empty));

        r.IsValid.Should().BeFalse("un rastro sin autor no auditaría nada.");
    }

    [Fact]
    public void Confirmar_ConTodoEnSuSitio_Valida()
    {
        var v = new ConfirmCryptoPaymentValidator();

        var r = v.Validate(new ConfirmCryptoPaymentCommand(
            "ORD-1",
            new ConfirmCryptoPaymentRequest { CryptoTransactionId = "a1b2c3d4e5f6", Notes = "Red BTC" },
            "usr-1", "admin@example.com"));

        r.IsValid.Should().BeTrue();
    }

    // ── Y que completar por cripto NO exija ya el identificador ─────────────────

    [Fact]
    public void CompletarPorCripto_YaNoExigeElIdentificadorDeTransaccion()
    {
        var v = new MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup
                    .CompleteSignupValidator();

        var r = v.Validate(new MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup
            .CompleteSignupCommand("ORD-1", new CompleteSignupRequest
            {
                PaymentMethod       = PaymentMethodType.Crypto,
                CryptoCurrency      = "BTC",
                CryptoTransactionId = null
            }));

        r.IsValid.Should().BeTrue(
            "nadie puede producir el hash al completar el alta: la transferencia todavía no se ha hecho.");
    }

    [Fact]
    public void CompletarPorCripto_SigueExigiendoLaMoneda()
    {
        var v = new MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup
                    .CompleteSignupValidator();

        var r = v.Validate(new MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.CompleteSignup
            .CompleteSignupCommand("ORD-1", new CompleteSignupRequest
            {
                PaymentMethod  = PaymentMethodType.Crypto,
                CryptoCurrency = null
            }));

        r.IsValid.Should().BeFalse("hay que saber en qué moneda se va a cobrar desde el principio.");
    }
}
