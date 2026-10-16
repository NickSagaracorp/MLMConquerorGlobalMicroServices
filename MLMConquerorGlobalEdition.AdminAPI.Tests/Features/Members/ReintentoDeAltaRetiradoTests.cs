using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

/// <summary>
/// <c>POST api/v1/admin/signups/{orderId}/retry-complete</c> reenviaba <c>complete</c> con
/// <c>paymentMethod: 1</c> (tarjeta) sin tarjeta para activar un alta "zombi" sin cobrar. La validación ya lo
/// rechazaba siempre y ningún método que cobra se puede repetir así (el token pide su código, la cripto su
/// moneda, Entrax su QR). Queda retirado: <c>410 SIGNUP_RETRY_RETIRED</c> sin llamar a SignupAPI.
/// </summary>
public class ReintentoDeAltaRetiradoTests
{
    [Fact]
    public async Task Reintentar_un_alta_es_410_y_no_llama_a_SignupAPI()
    {
        await using var db = InMemoryDbHelper.Create();
        var controlador = new AdminSignupsController(db);

        var r = controlador.RetryComplete("ORD-1");

        var objeto = r.Should().BeAssignableTo<ObjectResult>().Subject;
        objeto.StatusCode.Should().Be(StatusCodes.Status410Gone);
        var cuerpo = objeto.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        cuerpo.ErrorCode.Should().Be(AdminSignupsController.RetryRetiredCode);
        cuerpo.ErrorCode.Should().Be("SIGNUP_RETRY_RETIRED");
        (cuerpo.Message ?? string.Join(" ", cuerpo.Errors ?? [])).Should().Contain("Re-check");
    }

    [Fact]
    public void El_controlador_ya_no_necesita_el_cliente_de_SignupAPI()
        => typeof(AdminSignupsController).GetConstructors().Single().GetParameters()
            .Should().NotContain(p => p.ParameterType == typeof(IHttpClientFactory));
}
