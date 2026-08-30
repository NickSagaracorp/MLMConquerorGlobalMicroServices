namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Convierte un número de tarjeta en los identificadores opacos con los que el resto del producto
/// trabaja. Es la ÚNICA puerta por la que un PAN se transforma en algo guardable.
/// </summary>
/// <remarks>
/// POR QUÉ ESTÁ EN SharedKernel Y NO DONDE NACIÓ. Nació en
/// <c>BizCenter/Services/Billing</c>, y allí solo lo alcanzaba un consumidor: el alta de tarjeta
/// del centro de negocios. El alta de miembro —que es la otra mitad del producto que cobra con
/// tarjeta— no podía verlo, así que el asistente se inventó los tokens a mano
/// (<c>GatewayToken = "manual"</c>, <c>Gateway = "Stripe"</c>) y llevaba roto desde entonces.
///
/// SharedKernel es el único sitio que alcanzan LOS CUATRO consumidores que hay: SignupAPI,
/// BizCenter, la aplicación de alta (WASM) y, mañana, las MAUI. No entra aquí por comodidad: entra
/// porque no depende de alojamiento web —ni HttpContext, ni MediatR, ni base de datos—, que es la
/// regla escrita en el .csproj de este proyecto. Si algún día una implementación necesita un
/// servidor debajo, la implementación va a SharedKernel.Server; este contrato se queda aquí.
///
/// DÓNDE SE EJECUTA ESTO Y POR QUÉ IMPORTA (PCI DSS). La implementación se registra en el
/// contenedor del CLIENTE, no en el del servidor. El PAN se teclea en el navegador y se convierte
/// allí mismo en un token; a nuestra API viaja el token, nunca el número. Es lo que mantiene
/// nuestros servidores fuera del alcance de PCI DSS, y es también el flujo que exige la
/// implementación real: Stripe.js o el iframe de Spreedly hablan desde el navegador con la
/// pasarela sin pasar por nosotros. Por eso el contrato es <c>async</c> y lleva
/// <see cref="CancellationToken"/> aunque la implementación simulada no los necesite: la real es
/// una llamada de red desde el navegador, y una interfaz síncrona habría obligado a rehacer el
/// alta entera el día que se conecte la pasarela de verdad.
/// </remarks>
public interface ICardTokenizationService
{
    /// <summary>
    /// Entrega el número a la pasarela y devuelve lo único que puede salir de ahí: identificadores.
    /// </summary>
    /// <param name="rawCardNumber">
    /// El PAN. Quien implemente esto NO PUEDE registrarlo, guardarlo ni reenviarlo a ningún sitio
    /// que no sea la pasarela.
    /// </param>
    Task<TokenizationResult> TokenizeAsync(
        string rawCardNumber,
        int    expiryMonth,
        int    expiryYear,
        string cardholderName,
        string cvv,
        CancellationToken ct = default);

    /// <summary>Detecta la marca a partir de los primeros dígitos (BIN).</summary>
    /// <remarks>
    /// Está en la interfaz porque la pasarela real la sabe mejor que nosotros y puede querer
    /// devolver la suya. Mientras no lo haga, la implementación delega en
    /// <see cref="CardBrandDetector"/>, que es donde vive la única versión de esta función.
    /// </remarks>
    string DetectBrand(string rawCardNumber);
}

/// <summary>
/// Lo que la pasarela devuelve por una tarjeta. Son exactamente los tres campos que
/// <c>CreditCardInfoDto</c> exige, y ese emparejamiento no es casual: se comprueba en las pruebas.
/// </summary>
/// <param name="Gateway">
/// Identificador de la pasarela. EN MINÚSCULAS Y SIN ADORNOS: el validador de la API lo exige
/// (<c>^[a-z][a-z0-9]{1,29}$</c>), y ese es exactamente el motivo por el que el alta con tarjeta
/// llevaba meses rota — el asistente mandaba "Stripe", con mayúscula, escrito a fuego.
/// Este valor SIEMPRE sale de la pasarela; nadie lo escribe en el sitio donde se construye el pago.
/// </param>
/// <param name="GatewayToken">Referencia de un solo uso de la pasarela (nonce / PaymentMethod).</param>
/// <param name="CardToken">Referencia permanente del medio de pago, la que sirve para recobrar.</param>
public record TokenizationResult(
    string Gateway,
    string GatewayToken,
    string CardToken);
