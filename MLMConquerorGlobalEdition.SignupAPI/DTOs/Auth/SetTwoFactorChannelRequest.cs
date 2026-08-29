using MLMConquerorGlobalEdition.Domain.Entities.Security;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Cambia el canal por el que el usuario quiere recibir su código de segundo factor. Solo lleva
/// el canal: el usuario sale del token de acceso, nunca del cuerpo, para que nadie pueda tocar
/// la configuración de otra cuenta.
/// </summary>
public class SetTwoFactorChannelRequest
{
    /// <summary>
    /// Tiene que ser uno de los canales que <c>AvailableChannels</c> devuelve para esta cuenta.
    /// El servidor lo comprueba otra vez aunque la pantalla ya filtre: fijar un canal sin destino
    /// deja al usuario esperando un código que nunca sale y sin poder entrar.
    /// </summary>
    public TwoFactorChannel Channel { get; set; }
}
