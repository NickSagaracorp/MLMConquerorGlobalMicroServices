using System.Text.Json;
using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;
using MLMConquerorGlobalEdition.Signups.Services;

namespace MLMConquerorGlobalEdition.Signups.Tests.Billing;

/// <summary>
/// El alta con tarjeta: que se tokenice de verdad y que lo que sale de ahí sea exactamente lo que
/// la API acepta.
/// </summary>
/// <remarks>
/// EL FALLO QUE ESTAS PRUEBAS CIERRAN. El asistente mandaba <c>GatewayToken = "manual"</c> y
/// <c>Gateway = "Stripe"</c> escritos a fuego, y no mandaba <c>CardToken</c> ninguno. Las tres
/// cosas las rechaza <see cref="CreditCardInfoDtoValidator"/>: la vía de tarjeta devolvía 400 desde
/// el 23 de mayo de 2026 y nadie lo vio porque las altas se venían haciendo con token.
///
/// Ninguna prueba de las que había podía detectarlo: el validador se probaba con un DTO escrito a
/// mano en la propia prueba, así que comprobaba el validador contra sí mismo y nunca contra lo que
/// el asistente manda de verdad. Por eso lo que se comprueba aquí es el CAMINO ENTERO —tokenizar,
/// serializar, deserializar como lo hace la API, validar—, que es donde estaba el hueco.
/// </remarks>
public class TokenizacionDeTarjetaTests
{
    // Un Visa de prueba de los de toda la vida, escrito como lo teclea la gente: con espacios.
    private const string PanConEspacios = "4242 4242 4242 4242";

    private static readonly JsonSerializerOptions ComoLaApi = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Una pasarela de mentira que devuelve lo que se le diga. Sirve para comprobar que los tres
    /// identificadores SALEN DE AQUÍ y no de un literal escondido en la pantalla.
    /// </summary>
    private sealed class PasarelaDeMentira(string gateway, string gatewayToken, string cardToken)
        : ICardTokenizationService
    {
        public int Llamadas { get; private set; }
        public string? PanRecibido { get; private set; }

        public Task<TokenizationResult> TokenizeAsync(
            string rawCardNumber, int expiryMonth, int expiryYear,
            string cardholderName, string cvv, CancellationToken ct = default)
        {
            Llamadas++;
            PanRecibido = rawCardNumber;
            return Task.FromResult(new TokenizationResult(gateway, gatewayToken, cardToken));
        }

        public string DetectBrand(string rawCardNumber) => CardBrandDetector.Detect(rawCardNumber);
    }

    // ===============================================================================================
    // La detección de marca, que estaba escrita tres veces
    // ===============================================================================================

    [Theory]
    [InlineData("4242424242424242", "Visa")]
    [InlineData("4111 1111 1111 1111", "Visa")]
    [InlineData("5555555555554444", "Mastercard")]
    [InlineData("2223003122003222", "Mastercard")]   // rango 2221-2720, el que la copia del asistente daba por bueno con solo mirar el 2
    [InlineData("378282246310005", "Amex")]
    [InlineData("371449635398431", "Amex")]
    [InlineData("6011111111111117", "Discover")]
    [InlineData("3530111333300000", "JCB")]          // empieza por 3 y NO es Amex ni Diners
    [InlineData("30569309025904", "Diners")]
    [InlineData("2131000000000008", "JCB")]          // 21 no cae en el 22-27 de Mastercard: el orden importa
    [InlineData("9999999999999999", CardBrandDetector.Unknown)]
    [InlineData("", CardBrandDetector.Unknown)]
    public void LaMarca_SeDeduceDelBin(string pan, string marcaEsperada)
        => CardBrandDetector.Detect(pan).Should().Be(marcaEsperada);

    [Fact]
    public void LaMarca_NoLeAfectanLosSeparadores()
        => CardBrandDetector.Detect("4242-4242 4242.4242")
            .Should().Be(CardBrandDetector.Detect("4242424242424242"));

    /// <summary>
    /// La unificación, comprobada donde importa: la marca que devuelve el servicio de tokenización
    /// —la que acaba en <c>MemberCreditCards.CardBrand</c>— y la que calcula el detector —la que ve
    /// la persona mientras teclea— son la MISMA función, no dos que se parecen.
    /// </summary>
    [Theory]
    [InlineData("4242424242424242")]
    [InlineData("5555555555554444")]
    [InlineData("2223003122003222")]
    [InlineData("378282246310005")]
    [InlineData("6011111111111117")]
    [InlineData("3530111333300000")]
    [InlineData("30569309025904")]
    [InlineData("9999999999999999")]
    public void LaMarca_DelServicioYLaDelDetector_SonLaMisma(string pan)
        => new SimulatedCardTokenizationService().DetectBrand(pan)
            .Should().Be(CardBrandDetector.Detect(pan));

    /// <summary>
    /// Toda marca que salga de aquí tiene que pasar el patrón que la API exige. Si alguien añade
    /// una red nueva con un guión o un dígito en el nombre, el alta se rompería igual que se rompió
    /// con "Stripe" — y se rompería solo para las tarjetas de esa red.
    /// </summary>
    [Theory]
    [InlineData("4242424242424242")]
    [InlineData("5555555555554444")]
    [InlineData("378282246310005")]
    [InlineData("6011111111111117")]
    [InlineData("3530111333300000")]
    [InlineData("30569309025904")]
    [InlineData("9999999999999999")]
    public void LaMarca_SiemprePasaElValidadorDeLaApi(string pan)
    {
        var dto = DtoValido();
        dto.CardBrand = CardBrandDetector.Detect(pan);

        new CreditCardInfoDtoValidator().TestValidate(dto)
            .ShouldNotHaveValidationErrorFor(x => x.CardBrand);
    }

    // ===============================================================================================
    // La pasarela simulada
    // ===============================================================================================

    [Fact]
    public async Task LaPasarelaSimulada_DevuelveLosTresIdentificadoresNoVacios()
    {
        var r = await new SimulatedCardTokenizationService()
            .TokenizeAsync("4242424242424242", 12, DateTime.Now.Year + 2, "Ada Lovelace", "123");

        r.Gateway.Should().NotBeNullOrWhiteSpace();
        r.GatewayToken.Should().NotBeNullOrWhiteSpace();
        r.CardToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LaPasarelaSimulada_DaUnTokenDistintoCadaVez()
    {
        var servicio = new SimulatedCardTokenizationService();
        var a = await servicio.TokenizeAsync("4242424242424242", 12, 2030, "A", "123");
        var b = await servicio.TokenizeAsync("4242424242424242", 12, 2030, "A", "123");

        a.GatewayToken.Should().NotBe(b.GatewayToken, "un nonce que se repite no es un nonce");
        a.CardToken.Should().NotBe(b.CardToken);
    }

    /// <summary>
    /// El identificador de la pasarela simulada tiene que pasar el patrón de la API. Era
    /// "spreedly-simulated" y el guión lo habría tumbado exactamente igual que la mayúscula de
    /// "Stripe": el alta habría seguido rota después de arreglarla.
    /// </summary>
    [Fact]
    public void ElIdentificadorDeLaPasarelaSimulada_PasaElValidadorDeLaApi()
    {
        var dto = DtoValido();
        dto.Gateway = SimulatedCardTokenizationService.GatewayId;

        new CreditCardInfoDtoValidator().TestValidate(dto)
            .ShouldNotHaveValidationErrorFor(x => x.Gateway);
    }

    // ===============================================================================================
    // Lo que el asistente construye y manda
    // ===============================================================================================

    /// <summary>
    /// LA PRUEBA QUE FALLA SI ALGUIEN VUELVE A ESCRIBIR LA PASARELA A FUEGO. Los tres
    /// identificadores salen del resultado de la tokenización y de ningún otro sitio: se le da una
    /// pasarela que dice llamarse "braintree" y eso es lo que tiene que viajar.
    /// </summary>
    [Fact]
    public async Task ElPago_TomaLosTresIdentificadoresDeLaPasarelaYNoDeUnLiteral()
    {
        var pasarela = new PasarelaDeMentira("braintree", "nonce_de_la_pasarela", "card_de_la_pasarela");

        var pago = await CardPayment.PrepareAsync(
            pasarela, PanConEspacios, 12, DateTime.Now.Year + 2, "Ada Lovelace", "123");

        pago.Gateway.Should().Be("braintree");
        pago.GatewayToken.Should().Be("nonce_de_la_pasarela");
        pago.CardToken.Should().Be("card_de_la_pasarela");

        pago.Gateway.Should().NotBe("Stripe", "era el literal que rompía el alta");
        pago.GatewayToken.Should().NotBe("manual", "era el otro literal que rompía el alta");
        pasarela.Llamadas.Should().Be(1, "el pago se tokeniza de verdad, no se rellena a mano");
    }

    [Fact]
    public async Task ElPago_NuncaLlevaElCardTokenVacio()
    {
        var pago = await CardPayment.PrepareAsync(
            new SimulatedCardTokenizationService(), PanConEspacios, 12, DateTime.Now.Year + 2, "Ada", "123");

        pago.CardToken.Should().NotBeNullOrWhiteSpace(
            "el asistente no lo mandaba y CreditCardInfoDto lo exige con NotEmpty");
        pago.GatewayToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ElPago_LaPasarelaViajaEnMinusculas()
    {
        var pago = await CardPayment.PrepareAsync(
            new SimulatedCardTokenizationService(), PanConEspacios, 12, DateTime.Now.Year + 2, "Ada", "123");

        pago.Gateway.Should().Be(pago.Gateway.ToLowerInvariant(),
            "CreditCardInfoDtoValidator exige ^[a-z][a-z0-9]{1,29}$ y por eso 'Stripe' se rechazaba");
    }

    [Fact]
    public async Task ElPago_DerivaBinYUltimosCuatroDelNumeroTecleado()
    {
        var pago = await CardPayment.PrepareAsync(
            new PasarelaDeMentira("simulated", "t", "c"),
            PanConEspacios, 9, DateTime.Now.Year + 3, "Ada", "123");

        pago.First6.Should().Be("424242");
        pago.Last4.Should().Be("4242");
        pago.CardBrand.Should().Be("Visa");
        pago.ExpiryMonth.Should().Be(9);
        pago.ExpiryYear.Should().Be(DateTime.Now.Year + 3);
    }

    /// <summary>
    /// El PAN se le entrega a la pasarela ya limpio de separadores y no se guarda en el objeto que
    /// viaja: en <see cref="CreditCardPaymentInfo"/> no hay ni un campo donde pudiera caber.
    /// </summary>
    [Fact]
    public async Task ElPago_NoLlevaElNumeroDeTarjetaANingunSitio()
    {
        var pasarela = new PasarelaDeMentira("simulated", "t", "c");
        var pago = await CardPayment.PrepareAsync(
            pasarela, PanConEspacios, 12, DateTime.Now.Year + 2, "Ada", "123");

        pasarela.PanRecibido.Should().Be("4242424242424242");

        var json = JsonSerializer.Serialize(pago);
        json.Should().NotContain("4242424242424242",
            "el número completo no puede aparecer en el cuerpo que se manda a la API");
    }

    // ===============================================================================================
    // El contrato con la API, recorrido entero
    // ===============================================================================================

    /// <summary>
    /// EL CAMINO COMPLETO: se tokeniza como en el navegador, se serializa como lo hace
    /// <c>PostAsJsonAsync</c>, se deserializa como lo hace la API y se valida con el validador de
    /// verdad. Es la prueba que habría estado roja desde el 23 de mayo.
    /// </summary>
    [Fact]
    public async Task LoQueElAsistenteManda_LoAceptaElValidadorDeLaApi()
    {
        var pago = await CardPayment.PrepareAsync(
            new SimulatedCardTokenizationService(),
            PanConEspacios, 12, DateTime.Now.Year + 2, "Ada Lovelace", "123");

        var json = JsonSerializer.Serialize(pago);
        var dto  = JsonSerializer.Deserialize<CreditCardInfoDto>(json, ComoLaApi);

        dto.Should().NotBeNull();
        new CreditCardInfoDtoValidator().TestValidate(dto!).ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// La otra mitad de la misma prueba: con lo que el asistente mandaba ANTES, el validador falla.
    /// Sin esto, la de arriba podría estar verde por casualidad —por ejemplo si el validador
    /// dejara de exigir nada— y no probaría que el arreglo es el que arregla.
    /// </summary>
    [Fact]
    public void LoQueElAsistenteMandabaAntes_LoRechazaElValidadorDeLaApi()
    {
        var comoAntes = new CreditCardInfoDto
        {
            GatewayToken = "manual",
            CardToken    = string.Empty,      // no se mandaba
            Last4        = "4242",
            First6       = "424242",
            CardBrand    = "Visa",
            ExpiryMonth  = 12,
            ExpiryYear   = DateTime.Now.Year + 2,
            Gateway      = "Stripe"           // con mayúscula
        };

        var resultado = new CreditCardInfoDtoValidator().TestValidate(comoAntes);

        resultado.ShouldHaveValidationErrorFor(x => x.Gateway);
        resultado.ShouldHaveValidationErrorFor(x => x.CardToken);
    }

    /// <summary>
    /// Los dos tipos son el mismo contrato escrito a los dos lados de una frontera que no se puede
    /// cruzar con una referencia de proyecto —el asistente corre en WebAssembly y SignupAPI es una
    /// aplicación web—. Esta prueba es lo que sustituye a esa referencia: si alguien añade, quita o
    /// renombra un campo en un lado, se entera aquí y no con un 400 en producción.
    /// </summary>
    [Fact]
    public void ElBloqueDeTarjetaDelAsistente_TieneLosMismosCamposQueElDtoDeLaApi()
    {
        static IEnumerable<string> Campos(Type t) => t
            .GetProperties()
            .Select(p => $"{p.Name}:{p.PropertyType.Name}")
            .OrderBy(x => x, StringComparer.Ordinal);

        Campos(typeof(CreditCardPaymentInfo))
            .Should().BeEquivalentTo(Campos(typeof(CreditCardInfoDto)));
    }

    // ===============================================================================================
    // Lo que se comprueba antes de molestar a la pasarela
    // ===============================================================================================

    [Theory]
    [InlineData("", 12, 2030, "123")]                       // sin número
    [InlineData("4242", 12, 2030, "123")]                   // demasiado corto
    [InlineData("42424242424242424242", 12, 2030, "123")]   // demasiado largo
    [InlineData("4242424242424242", 0, 2030, "123")]        // sin mes
    [InlineData("4242424242424242", 13, 2030, "123")]       // mes imposible
    [InlineData("4242424242424242", 12, 0, "123")]          // sin año
    [InlineData("4242424242424242", 12, 2030, "")]          // sin CVV
    [InlineData("4242424242424242", 12, 2030, "12")]        // CVV corto
    public void LaComprobacionPrevia_AvisaEnVezDeDejarQueLaApiDevuelvaUn400(
        string pan, int mes, int anio, string cvv)
        => CardPayment.Validate(pan, mes, anio, cvv).Should().NotBeNullOrWhiteSpace();

    [Fact]
    public void LaComprobacionPrevia_DejaPasarUnaTarjetaBienFormada()
        => CardPayment.Validate(PanConEspacios, 12, DateTime.Now.Year + 2, "123").Should().BeNull();

    // ===============================================================================================
    // El guardián del servidor
    // ===============================================================================================

    /// <summary>
    /// Tokenizar en el servidor obligaría a que el número de tarjeta viajara hasta él. El asistente
    /// se prepinta de ese lado, así que el contenedor necesita una implementación; la que hay lanza.
    /// </summary>
    [Fact]
    public async Task ElServidorNoTokeniza_LanzaSiAlguienLoIntenta()
    {
        var guardian = new ServerSideCardTokenizationGuard();

        var acto = () => guardian.TokenizeAsync("4242424242424242", 12, 2030, "Ada", "123");

        await acto.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no puede tokenizar tarjetas en el servidor*");
    }

    /// <summary>
    /// La marca sí se calcula de ese lado: el marcado la llama para pintar la etiqueta y, si
    /// lanzara, la página no se podría prepintar.
    /// </summary>
    [Fact]
    public void ElServidorSiCalculaLaMarca_ParaPoderPrepintarLaPagina()
    {
        var guardian = new ServerSideCardTokenizationGuard();

        guardian.DetectBrand(string.Empty).Should().Be(CardBrandDetector.Unknown);
        guardian.DetectBrand("4242424242424242").Should().Be("Visa");
    }

    private static CreditCardInfoDto DtoValido() => new()
    {
        GatewayToken = "tok_abc123",
        CardToken    = "card_def456",
        Last4        = "4242",
        First6       = "424242",
        CardBrand    = "Visa",
        ExpiryMonth  = 12,
        ExpiryYear   = DateTime.Now.Year + 2,
        Gateway      = "stripe"
    };
}
