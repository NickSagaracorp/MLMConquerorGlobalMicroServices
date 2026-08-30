using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Signups.Services;

/// <summary>
/// El guardián del lado servidor: aquí NO se tokeniza. Si alguien lo intenta, esto revienta.
/// </summary>
/// <remarks>
/// POR QUÉ EXISTE. La tokenización del alta corre en el navegador
/// (<c>Signups.Client/Program.cs</c>) para que el número de tarjeta no cruce la red hacia nosotros
/// —es la decisión que mantiene esta infraestructura fuera del alcance de PCI DSS—. Pero el
/// asistente se PREPINTA en el servidor, y prepintarlo obliga a construir el componente, y
/// construirlo obliga a resolver todo lo que inyecta. Sin una implementación registrada de este
/// lado, la primera carga del alta se caería con un error de contenedor.
///
/// Registrar aquí la simulada habría hecho que compilara y funcionara... tokenizando en el
/// servidor en cuanto alguien devolviera el asistente a un modo con circuito. Esta clase es la
/// alternativa: satisface al contenedor, deja pasar lo que es inofensivo y convierte en un fallo
/// ruidoso —en desarrollo, en la primera prueba— lo que sería una fuga silenciosa de PAN.
///
/// SI ESTA EXCEPCIÓN SALTA EN PRODUCCIÓN significa que el asistente ha vuelto a ejecutarse en el
/// servidor con el número de tarjeta delante. Lo que hay que arreglar es el modo de render de la
/// página, no esta clase.
/// </remarks>
public sealed class ServerSideCardTokenizationGuard : ICardTokenizationService
{
    public Task<TokenizationResult> TokenizeAsync(
        string rawCardNumber,
        int    expiryMonth,
        int    expiryYear,
        string cardholderName,
        string cvv,
        CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "El alta no puede tokenizar tarjetas en el servidor: el número tendría que haber " +
            "viajado hasta aquí. La tokenización se registra en el contenedor de WebAssembly " +
            "(Signups.Client/Program.cs) y las páginas de alta se pintan con " +
            "@rendermode InteractiveWebAssembly precisamente para que esto no ocurra. " +
            "Si ves esto, revisa el modo de render de Signup.razor / MemberJoin.razor.");

    /// <summary>
    /// La marca SÍ se puede calcular de este lado: es aritmética sobre el BIN y durante el
    /// prepintado el campo está vacío. El marcado la llama para pintar la etiqueta de la marca, así
    /// que si esto lanzara no se podría prepintar la página.
    /// </summary>
    public string DetectBrand(string rawCardNumber) => CardBrandDetector.Detect(rawCardNumber);
}
