namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// El bloque de tarjeta que viaja al completar un alta. Es el gemelo de
/// <c>SignupAPI.DTOs.CreditCardInfoDto</c>, campo por campo.
/// </summary>
/// <remarks>
/// POR QUÉ HAY UN TIPO Y NO UN OBJETO ANÓNIMO. Antes esto se construía con un <c>new { ... }</c>
/// dentro del asistente, y ahí es donde se colaron los dos valores a fuego que rompieron el alta
/// —<c>GatewayToken = "manual"</c> y <c>Gateway = "Stripe"</c>— y donde faltaba <c>CardToken</c>
/// entero. Un objeto anónimo no se puede probar, no se puede comparar con el DTO del otro lado y
/// no obliga a rellenar nada: se equivoca en silencio y el fallo aparece como un 400 genérico.
/// Con un tipo, el contrato se comprueba en una prueba y la construcción pasa por
/// <see cref="CardPayment.PrepareAsync"/>, que es el único sitio donde se decide de dónde sale
/// cada campo.
///
/// POR QUÉ NO SE REUTILIZA EL DTO DE LA API. <c>CreditCardInfoDto</c> vive en SignupAPI, que es una
/// aplicación web; el asistente corre en WebAssembly y no puede referenciarla. La pareja se vigila
/// con una prueba de paridad de propiedades en vez de con una referencia de proyecto.
/// </remarks>
public sealed record CreditCardPaymentInfo
{
    /// <summary>Referencia de un solo uso de la pasarela. Sale de la tokenización, nunca a mano.</summary>
    public required string GatewayToken { get; init; }

    /// <summary>Referencia permanente del medio de pago. Sale de la tokenización, nunca a mano.</summary>
    public required string CardToken { get; init; }

    /// <summary>Últimos 4 dígitos: lo único del número que se enseña en claro.</summary>
    public required string Last4 { get; init; }

    /// <summary>Primeros 6 dígitos (BIN).</summary>
    public required string First6 { get; init; }

    /// <summary>Marca, salida de <see cref="ICardTokenizationService.DetectBrand"/>.</summary>
    public required string CardBrand { get; init; }

    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }

    /// <summary>Identificador de la pasarela, en minúsculas. Sale de la tokenización, nunca a mano.</summary>
    public required string Gateway { get; init; }
}

/// <summary>
/// El único sitio donde se arma el bloque de tarjeta de un alta.
/// </summary>
/// <remarks>
/// Existe para que "de dónde sale cada campo" sea una decisión escrita una vez y comprobable, en
/// lugar de repetirse en cada pantalla que cobre con tarjeta —hoy dos, <c>Signup.razor</c> y
/// <c>MemberJoin.razor</c>, que tenían el mismo bloque copiado con los mismos dos errores—.
///
/// LO QUE ESTA CLASE GARANTIZA: <c>Gateway</c>, <c>GatewayToken</c> y <c>CardToken</c> salen los
/// tres del resultado de la tokenización y de ningún otro sitio. Si alguien vuelve a escribir el
/// nombre de la pasarela a mano, tiene que saltarse esto para conseguirlo, y hay una prueba que
/// lee el código de los dos asistentes para que tampoco pueda.
/// </remarks>
public static class CardPayment
{
    /// <summary>
    /// Tokeniza y devuelve lo que hay que mandar a la API.
    /// </summary>
    /// <remarks>
    /// EL PAN ENTRA AQUÍ Y NO SALE. De él solo se derivan el BIN, los últimos cuatro y la marca;
    /// el número completo se le da a <paramref name="tokenizer"/> y nada más. Como esto corre en el
    /// navegador, el número no llega a cruzar la red hacia nosotros.
    /// </remarks>
    public static async Task<CreditCardPaymentInfo> PrepareAsync(
        ICardTokenizationService tokenizer,
        string                   rawCardNumber,
        int                      expiryMonth,
        int                      expiryYear,
        string                   cardholderName,
        string                   cvv,
        CancellationToken        ct = default)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);

        var digits = CardBrandDetector.OnlyDigits(rawCardNumber);

        var tokenization = await tokenizer.TokenizeAsync(
            digits, expiryMonth, expiryYear, cardholderName ?? string.Empty, cvv ?? string.Empty, ct);

        return new CreditCardPaymentInfo
        {
            // Los tres que solo puede decidir la pasarela.
            Gateway      = tokenization.Gateway,
            GatewayToken = tokenization.GatewayToken,
            CardToken    = tokenization.CardToken,

            // Los derivados del número, que se quedan aquí.
            First6      = digits.Length >= 6 ? digits[..6]  : digits,
            Last4       = digits.Length >= 4 ? digits[^4..] : digits,
            CardBrand   = tokenizer.DetectBrand(digits),
            ExpiryMonth = expiryMonth,
            ExpiryYear  = expiryYear
        };
    }

    /// <summary>
    /// Comprueba lo que la persona ha tecleado ANTES de molestar a la pasarela. Devuelve el aviso
    /// a enseñar, o <c>null</c> si la tarjeta está bien formada.
    /// </summary>
    /// <remarks>
    /// Está aquí y no en la pantalla porque los dos asistentes necesitan exactamente lo mismo, y
    /// porque sin esto un número vacío llegaba a la API y volvía como un 400 genérico —"Signup
    /// failed"— que no le dice nada a nadie. El rango 13-19 es el de
    /// <c>ValidationPatterns.CreditCardPanPattern</c>, y el de CVV el de
    /// <c>CreditCardCvvPattern</c>: es el mismo contrato, comprobado antes de salir.
    /// </remarks>
    public static string? Validate(string? rawCardNumber, int expiryMonth, int expiryYear, string? cvv)
    {
        var digits = CardBrandDetector.OnlyDigits(rawCardNumber);
        if (digits.Length is < 13 or > 19)
            return "Please enter a valid card number (13 to 19 digits).";

        if (expiryMonth is < 1 or > 12)
            return "Please select the card's expiration month.";

        if (expiryYear < 1)
            return "Please select the card's expiration year.";

        var cvvDigits = CardBrandDetector.OnlyDigits(cvv);
        if (cvvDigits.Length is < 3 or > 4)
            return "Please enter the card's 3 or 4 digit security code.";

        return null;
    }
}
