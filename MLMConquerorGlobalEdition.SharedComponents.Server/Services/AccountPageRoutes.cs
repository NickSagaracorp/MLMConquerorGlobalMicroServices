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
