using System.Reflection;
using MLMConquerorGlobalEdition.BizCenter.Features.Billing.AddCreditCard;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La tokenización de tarjeta se escribe UNA VEZ y la usan todos: no vuelve a haber una copia
/// dentro de BizCenter.
/// </summary>
/// <remarks>
/// DE DÓNDE VIENE ESTO. <c>ICardTokenizationService</c> y su implementación simulada nacieron en
/// <c>BizCenter/Services/Billing</c>. Estaban bien escritas y devolvían exactamente los tres campos
/// que la API del alta exige — pero desde el alta no se alcanzaban, porque BizCenter es otra
/// aplicación. Con la pieza fuera de su alcance, el asistente de alta hizo lo único que podía:
/// inventarse los valores a mano. El resultado fue una vía de pago rota durante meses.
///
/// Ahora la pieza vive en SharedKernel, que es el único proyecto que alcanzan a la vez SignupAPI,
/// BizCenter, la aplicación de alta en WebAssembly y las MAUI que vienen después. Lo que esta
/// prueba impide es que se deshaga: que alguien vuelva a crear una copia local "porque es más
/// cómodo" y las dos empiecen a divergir otra vez.
///
/// SI SE PONE EN ROJO: la copia se borra y se usa la de SharedKernel. Si a BizCenter le hiciera
/// falta algo que la compartida no da, se amplía la compartida.
/// </remarks>
public class TokenizacionCompartidaTests
{
    private static Assembly BizCenterAssembly => typeof(AddCreditCardHandler).Assembly;

    /// <summary>
    /// El alta de tarjeta del centro de negocios depende del contrato COMPARTIDO. Si alguien
    /// introdujera una copia local, el parámetro apuntaría a otro ensamblado y esto se pondría rojo.
    /// </summary>
    [Fact]
    public void ElAltaDeTarjeta_DependeDelContratoDeSharedKernel()
    {
        var tokenizador = typeof(AddCreditCardHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType.Name == nameof(ICardTokenizationService))
            .ToArray();

        tokenizador.Should().HaveCount(1,
            "AddCreditCardHandler tokeniza, así que tiene que pedir el servicio por el constructor");

        tokenizador[0].ParameterType.Should().Be<ICardTokenizationService>();
        tokenizador[0].ParameterType.Assembly.GetName().Name
            .Should().Be("MLMConquerorGlobalEdition.SharedKernel");
    }

    /// <summary>
    /// Dentro de BizCenter no queda ni un tipo de tokenización propio. Es lo que convierte "está en
    /// SharedKernel" en "está SOLO en SharedKernel".
    /// </summary>
    [Fact]
    public void BizCenter_NoVuelveATenerSuPropiaCopiaDeLaTokenizacion()
    {
        var copias = BizCenterAssembly
            .GetTypes()
            .Where(t => t.Name is nameof(ICardTokenizationService)
                              or nameof(SimulatedCardTokenizationService)
                              or nameof(TokenizationResult)
                              or nameof(CardBrandDetector))
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        copias.Should().BeEmpty(
            "estas piezas viven en MLMConquerorGlobalEdition.SharedKernel.Billing y solo ahí; una " +
            "copia local es exactamente lo que dejó al alta de miembro sin poder tokenizar");
    }

    /// <summary>
    /// La detección de marca que acaba en <c>MemberCreditCards.CardBrand</c> es la misma función
    /// que ve la persona mientras teclea en el asistente de alta. Aquí se comprueba desde el lado
    /// del centro de negocios, que es el otro consumidor.
    /// </summary>
    [Theory]
    [InlineData("4242424242424242", "Visa")]
    [InlineData("5555555555554444", "Mastercard")]
    [InlineData("378282246310005", "Amex")]
    [InlineData("6011111111111117", "Discover")]
    [InlineData("9999999999999999", CardBrandDetector.Unknown)]
    public void LaMarcaQueSeGuarda_SaleDelDetectorCompartido(string pan, string esperada)
    {
        new SimulatedCardTokenizationService().DetectBrand(pan).Should().Be(esperada);
        CardBrandDetector.Detect(pan).Should().Be(esperada);
    }
}
