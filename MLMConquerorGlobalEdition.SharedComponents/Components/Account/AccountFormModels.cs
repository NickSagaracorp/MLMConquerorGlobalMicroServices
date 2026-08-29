namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account;

/// <summary>
/// Lo que lleva dentro cada formulario del área de cuenta, para que el modo interactivo pueda
/// entregárselo a la página sin que esta tenga que leer el DOM.
///
/// LOS NOMBRES DE LAS PROPIEDADES SON LOS MISMOS QUE LOS <c>name=</c> DE LOS CAMPOS, y por tanto
/// los mismos que los registros de <c>AccountEndpoints</c> en SharedComponents.Server. No es
/// casualidad ni cortesía: es lo que hace que las dos formas de enviar el mismo formulario lleven
/// exactamente los mismos datos con los mismos nombres, y que se pueda leer un componente y saber
/// qué llega al otro lado sin abrir el manejador del POST.
///
/// Son clases con propiedades de lectura y escritura, y no <c>record</c> con <c>init</c>, porque
/// <c>@bind</c> escribe sobre ellas mientras el usuario teclea.
///
/// NO SE REUTILIZAN LOS REGISTROS DE <c>AccountEndpoints</c>: aquellos viven en el proyecto de
/// servidor, que una MAUI no puede referenciar. Duplicar cuatro nombres de campo es el precio de
/// que esta biblioteca siga entrando en un APK.
///
/// El reto (<c>ChallengeToken</c>) no aparece en ninguno de estos modelos, igual que no aparece en
/// ningún campo del formulario. En web viaja en una cookie HttpOnly que pone el manejador del
/// POST; en móvil tendrá que guardarlo la página que hace la llamada. En los dos casos es asunto
/// de quien habla con la API, no de la pantalla que pide seis dígitos.
/// </summary>
public sealed class ForgotPasswordFormModel
{
    /// <summary>Correo de la cuenta que quiere recuperarse.</summary>
    public string Email { get; set; } = string.Empty;
}

/// <inheritdoc cref="ForgotPasswordFormModel"/>
public sealed class ResetPasswordFormModel
{
    /// <summary>Identificador del usuario, tal cual venía en el enlace del correo.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Token de recuperación, tal cual venía en el enlace del correo.</summary>
    public string Token { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// La segunda casilla. Viaja aunque la API no la reciba: el componente ya ha comprobado que
    /// coincide, y quien haga la llamada tiene delante lo mismo que tecleó el usuario.
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <inheritdoc cref="ForgotPasswordFormModel"/>
public sealed class ChangePasswordFormModel
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    /// <inheritdoc cref="ResetPasswordFormModel.ConfirmPassword"/>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <inheritdoc cref="ForgotPasswordFormModel"/>
public sealed class SetPasswordFormModel
{
    public string NewPassword { get; set; } = string.Empty;

    /// <inheritdoc cref="ResetPasswordFormModel.ConfirmPassword"/>
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <inheritdoc cref="ForgotPasswordFormModel"/>
public sealed class PhoneFormModel
{
    /// <summary>Número en E.164: '+', prefijo de país y dígitos seguidos.</summary>
    public string PhoneE164 { get; set; } = string.Empty;
}

/// <summary>
/// Los seis dígitos, sean del SMS, del correo o de la aplicación autenticadora. Uno solo para las
/// tres pantallas por lo mismo que las tres comparten campo, atributos y claves de recurso: son la
/// misma pregunta.
/// </summary>
/// <inheritdoc cref="ForgotPasswordFormModel"/>
public sealed class CodeFormModel
{
    public string Code { get; set; } = string.Empty;
}

/// <inheritdoc cref="ForgotPasswordFormModel"/>
public sealed class TwoFactorChannelFormModel
{
    /// <summary>Canal elegido: <c>Email</c>, <c>Sms</c> o <c>Authenticator</c>.</summary>
    public string Channel { get; set; } = string.Empty;
}
