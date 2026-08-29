namespace MLMConquerorGlobalEdition.ClientCore;

/// <summary>
/// De dónde sale el token de acceso del usuario que está usando la aplicación.
///
/// Existe para que <see cref="AuthApiGateway"/> no tenga que saberlo. Cada anfitrión lo guarda
/// donde puede: un portal web lo lleva en un claim de la cookie de sesión —que es HttpOnly y solo
/// el servidor puede leer—, y una aplicación móvil lo guarda en el almacenamiento seguro del
/// dispositivo. Son dos sitios que no se parecen en nada, pero al gateway le da igual cuál sea:
/// lo único que necesita es la cadena para poner el Bearer.
///
/// Es lo ÚNICO que ataba el gateway al alojamiento web. Con esto de por medio, la lógica de
/// hablar con SignupAPI —montar la petición, desenvolver el sobre, traducir el fallo a un código—
/// se escribe una vez y sirve a los portales y a las aplicaciones MAUI que vienen después.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// El token de acceso del usuario actual, o <c>null</c> si no hay sesión.
    /// </summary>
    /// <remarks>
    /// Asíncrono por el lado de móvil, no por el de web: el almacenamiento seguro del dispositivo
    /// se lee con una API asíncrona, y una firma síncrona obligaría a bloquear sobre ella, que es
    /// exactamente el patrón que congela la interfaz. Web devuelve el claim que ya tiene en
    /// memoria, así que su <see cref="ValueTask{TResult}"/> se completa sin ceder el hilo y no
    /// paga ninguna reserva.
    ///
    /// Devolver <c>null</c> es una respuesta válida y no un fallo: para el usuario, no tener token
    /// es lo mismo que tener la sesión caducada, y quien llama ya sabe qué hacer con eso.
    /// </remarks>
    ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default);
}
