namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Dónde vive cada pantalla del área de cuenta en el portal que monta esta superficie.
///
/// Es lo ÚNICO que cambia entre un portal y otro: los manejadores, el gateway y la carga de datos
/// son idénticos, pero el portal de administración sirve sus páginas bajo <c>/admin/…</c> y el
/// centro de negocios lo hará bajo el suyo. Con las rutas escritas a fuego en
/// <see cref="AccountEndpoints"/>, montar la misma superficie en el segundo portal habría exigido
/// copiar las cuatrocientas líneas del archivo para cambiarles ocho constantes — y a partir de ahí,
/// dos copias que se separan.
///
/// Cada ruta es a la vez el destino del éxito y el del error, así que se escribe una sola vez: una
/// de las dos puesta a mano en otro sitio es una redirección que se queda atrás el día que la ruta
/// cambie.
/// </summary>
public sealed record AccountPageRoutes
{
    // -------------------------------------------------------------------------------------------
    //  Anónimas — el usuario todavía no tiene sesión.
    // -------------------------------------------------------------------------------------------

    /// <summary>Formulario que pide el correo de recuperación.</summary>
    public required string ForgotPasswordPage { get; init; }

    /// <summary>Acuse de "si esa cuenta existe, te hemos escrito".</summary>
    public required string ForgotPasswordSentPage { get; init; }

    /// <summary>Formulario de contraseña nueva, al que llega el enlace del correo.</summary>
    public required string ResetPasswordPage { get; init; }

    /// <summary>Acuse de contraseña cambiada.</summary>
    public required string ResetPasswordDonePage { get; init; }

    /// <summary>
    /// La pantalla de login de este portal.
    /// </summary>
    /// <remarks>
    /// EL ÁREA DE CUENTA NO LA NECESITABA HASTA AHORA, y por eso no estaba: todo lo de aquí ocurre
    /// con la sesión ya firmada y volvía siempre a una pantalla de gestión. Dejó de ser cierto
    /// cuando las operaciones que cambian la postura de seguridad de la cuenta —contraseña,
    /// teléfono confirmado o retirado, segundo factor apagado— empezaron a REVOCAR el refresco en la
    /// API: a partir de ahí la sesión desde la que se hizo el cambio ya no se puede renovar, así que
    /// el destino honesto es el login y no el perfil.
    ///
    /// Va aquí y no se toma de <c>AuthPortalOptions</c> a propósito, aunque los dos portales de hoy
    /// monten las dos superficies: el área de cuenta puede montarse sin la puerta, y depender de
    /// aquello convertiría una decisión de pantallas en una dependencia entre dos superficies que
    /// están separadas justamente para no tenerla. Es el mismo valor, escrito en el mismo
    /// <c>Program.cs</c>, dos líneas más arriba.
    /// </remarks>
    public required string LoginPage { get; init; }

    // -------------------------------------------------------------------------------------------
    //  De gestión — requieren sesión.
    // -------------------------------------------------------------------------------------------

    /// <summary>Índice del área de cuenta; destino por defecto de casi todo lo que sale bien.</summary>
    public required string ProfilePage { get; init; }

    /// <summary>Cambiar o fijar la contraseña.</summary>
    public required string PasswordPage { get; init; }

    /// <summary>Alta y baja del teléfono.</summary>
    public required string PhonePage { get; init; }

    /// <summary>Confirmación del teléfono con el código del SMS.</summary>
    public required string PhoneVerifyPage { get; init; }

    /// <summary>
    /// Panel del segundo factor: canal preferido y baja. Destino de las dos acciones que postean
    /// desde <c>TwoFactorPanel</c>, salgan bien o mal.
    /// </summary>
    /// <remarks>
    /// No estaba en el grupo original porque el área no ofrecía esas dos acciones: el comentario
    /// de la pantalla decía que SignupAPI no exponía rutas para ellas. Sí las expone
    /// —<c>POST /api/v1/auth/two-factor/channel</c> y <c>/two-factor/disable</c>—, así que el
    /// panel dejó de ser de solo lectura y necesita, como las otras cinco pantallas, saber a dónde
    /// vuelve el usuario después de pulsar.
    /// </remarks>
    public required string SecurityPage { get; init; }

    /// <summary>
    /// Pantalla de datos personales, a la que vuelve la descarga cuando falla.
    /// </summary>
    /// <remarks>
    /// No estaba en el grupo de constantes del principio del archivo: vivía escrita a mano dentro
    /// de <c>DownloadPersonalDataAsync</c>, en los dos sitios donde esa descarga puede fallar. Es
    /// una ruta de pantalla como las otras ocho y se parametriza igual; dejarla a fuego habría
    /// mandado al usuario del segundo portal a una URL de administración.
    /// </remarks>
    public required string PersonalDataPage { get; init; }
}
