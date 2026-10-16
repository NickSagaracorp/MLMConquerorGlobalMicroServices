using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.Controllers;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// <c>GET api/v1/signups/payment-options</c> dice a la pantalla del alta qué métodos dependen de la
/// configuración: los países de Entrax y si la tarjeta está encendida (<c>Signup:CardEnabled</c>).
/// </summary>
public class OpcionesDePagoDelAltaTests
{
    private static EntraxSignupStatusService Servicio(IConfiguration? config)
        => new(InMemoryDbHelper.Create(), Mock.Of<IEntraxSignupSettlement>(), EscenarioEntrax.Opciones(),
               EscenarioEntrax.Reloj().Object, NullLogger<EntraxSignupStatusService>.Instance, config);

    [Fact]
    public void Sin_la_opcion_la_tarjeta_va_apagada()
    {
        Servicio(null).GetPaymentOptions().CardEnabled.Should().BeFalse();
        Servicio(TarjetaDelAlta.SinOpcion()).GetPaymentOptions().CardEnabled.Should().BeFalse();
    }

    [Fact]
    public void Apagada_y_encendida_se_reflejan_tal_cual()
    {
        Servicio(TarjetaDelAlta.Apagada()).GetPaymentOptions().CardEnabled.Should().BeFalse();
        Servicio(TarjetaDelAlta.Encendida()).GetPaymentOptions().CardEnabled.Should().BeTrue();
    }

    [Fact]
    public void La_tarjeta_no_cambia_los_paises_de_Entrax()
        => Servicio(TarjetaDelAlta.Apagada()).GetPaymentOptions().EntraxCountries.Should().Equal("RU");

    [Fact]
    public void La_respuesta_lleva_cardEnabled()
    {
        var estado = new Mock<IEntraxSignupStatusService>();
        estado.Setup(s => s.GetPaymentOptions())
              .Returns(new SignupPaymentOptionsDto { EntraxCountries = ["RU"], CardEnabled = false });
        var controlador = new EntraxSignupsController(Mock.Of<IEntraxCallbackProcessor>(), estado.Object,
            Mock.Of<IEntraxSignupSettlement>(), NullLogger<EntraxSignupsController>.Instance);

        var cuerpo = controlador.GetPaymentOptions().Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ApiResponse<SignupPaymentOptionsDto>>().Subject;

        var json = JsonSerializer.Serialize(cuerpo, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data").GetProperty("cardEnabled").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("data").GetProperty("entraxCountries")[0].GetString().Should().Be("RU");
    }
}
