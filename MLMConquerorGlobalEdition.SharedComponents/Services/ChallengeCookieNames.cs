namespace MLMConquerorGlobalEdition.SharedComponents.Services;

/// <summary>
/// Cómo se llaman en ESTE portal las tres cookies de reto que emite y canjea el área de cuenta.
///
/// Es lo único que cambia de un portal a otro: las opciones de la cookie —<c>HttpOnly</c>,
/// <c>Secure</c>, <c>SameSite</c>, <c>Path</c> y la ventana de diez minutos— son idénticas en
/// administración y en el centro de negocios y siguen viviendo una sola vez en
/// <see cref="ChallengeCookies"/>. Aquí solo están los nombres.
///
/// Se parametrizan por el mismo motivo que <see cref="AccountPageRoutes"/>, pero con una
/// consecuencia peor: una ruta mal puesta manda al usuario a una pantalla que no existe y se ve al
/// primer intento; un nombre de cookie que no coincide entre quien la ESCRIBE y quien la LEE no
/// falla al compilar, no falla en las pruebas y se manifiesta como "no hay ningún reto en curso"
/// después de que el usuario haya tecleado un código correcto. Es exactamente lo que pasaba con la
/// constante privada <c>2fa_challenge</c> del centro de negocios frente a los <c>mlm_admin_*</c> de
/// esta biblioteca.
/// </summary>
/// <remarks>
/// Los tres nombres son deliberadamente distintos entre sí dentro de un mismo portal: un usuario
/// puede tener un alta de teléfono a medias y abrir el login en otra pestaña, y ahí las cookies
/// conviven. Y son distintos entre portales porque con <c>Path = "/"</c> y un mismo dominio para
/// <c>/admin</c> y el centro de negocios, un reto de uno pisaría el del otro.
/// </remarks>
public sealed record ChallengeCookieNames
{
    /// <summary>ChallengeToken del segundo factor del login.</summary>
    public required string Login { get; init; }

    /// <summary>
    /// EnrollmentToken. Deliberadamente distinta de <see cref="Login"/>: son propósitos distintos
    /// y compartir nombre invita a redimir uno donde va el otro.
    /// </summary>
    public required string Enrollment { get; init; }

    /// <summary>
    /// ChallengeToken del alta de teléfono, que se canjea en <c>/account/phone/verify</c>.
    /// </summary>
    public required string Phone { get; init; }
}
