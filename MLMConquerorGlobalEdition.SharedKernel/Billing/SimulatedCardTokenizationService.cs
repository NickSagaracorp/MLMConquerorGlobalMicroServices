namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// La pasarela de mentira. Devuelve identificadores con la pinta correcta para que todo el camino
/// —alta, guardado de tarjeta, cobro recurrente— se pueda recorrer entero sin sandbox.
/// </summary>
/// <remarks>
/// SIRVE PARA DESARROLLO. En producción se sustituye el registro por la implementación real contra
/// la pasarela, que corre en el navegador (Stripe.js / iframe de Spreedly). No hace falta tocar ni
/// el asistente ni <c>AddCreditCardHandler</c>: los dos hablan con
/// <see cref="ICardTokenizationService"/>, no con esta clase.
///
/// NO REGISTRA NI GUARDA EL PAN, aunque lo reciba. Es a propósito y es la parte que hay que
/// conservar si alguien la modifica: mientras esta implementación corra en el navegador —que es
/// donde se registra— el número no sale del dispositivo de la persona, y por eso el camino de
/// desarrollo no empeora el alcance de PCI DSS respecto del de producción.
/// </remarks>
public class SimulatedCardTokenizationService : ICardTokenizationService
{
    /// <summary>
    /// Cómo se identifica esta "pasarela" en <c>MemberCreditCards.Gateway</c>.
    /// </summary>
    /// <remarks>
    /// EN MINÚSCULAS Y SIN GUIÓN, y no es cosmético: <c>CreditCardInfoDtoValidator</c> exige
    /// <c>^[a-z][a-z0-9]{1,29}$</c>, así que el valor anterior —"spreedly-simulated"— habría
    /// tumbado el alta con un 400 igual que lo hacía "Stripe". La constante existe para que ese
    /// contrato se pueda comprobar en una prueba en vez de descubrirse en caliente.
    /// </remarks>
    public const string GatewayId = "simulated";

    public Task<TokenizationResult> TokenizeAsync(
        string rawCardNumber,
        int    expiryMonth,
        int    expiryYear,
        string cardholderName,
        string cvv,
        CancellationToken ct = default)
    {
        // Los prefijos imitan a los de las pasarelas reales para que nada aguas abajo se acostumbre
        // a un formato que luego no va a llegar.
        return Task.FromResult(new TokenizationResult(
            Gateway:      GatewayId,
            GatewayToken: "tok_" + Guid.NewGuid().ToString("N")[..24],
            CardToken:    "card_" + Guid.NewGuid().ToString("N")));
    }

    /// <inheritdoc />
    public string DetectBrand(string rawCardNumber) => CardBrandDetector.Detect(rawCardNumber);
}
