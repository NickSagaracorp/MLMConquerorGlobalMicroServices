using Microsoft.AspNetCore.Components;

namespace MLMConquerorGlobalEdition.SharedComponents.Components.Account;

/// <summary>
/// Lo que comparten los formularios del área de cuenta para poder enviarse de dos maneras sin
/// escribir el marcado dos veces.
///
/// <see cref="AccountForm"/> pone el <c>&lt;form&gt;</c>; esta clase pone lo que cada pantalla
/// necesita alrededor: de dónde sale el código de error que se enseña, que no se pueda enviar dos
/// veces seguidas, y la traducción de "la página me pasó una devolución de llamada" a "el
/// formulario tiene que enviarse sin recargar".
///
/// POR QUÉ <see cref="SubmitFor(EventCallback, Func{string?})"/> DEVUELVE <c>default</c> Y NO UNA
/// DEVOLUCIÓN VACÍA: <see cref="AccountForm"/> decide su modo mirando
/// <c>EventCallback.HasDelegate</c>. Si aquí se devolviera siempre una devolución con delegado,
/// todos los formularios pasarían a modo interactivo en cuanto se montara este código y AdminWeb
/// —que no pasa ninguna— dejaría de postear. El <c>default</c> es lo que mantiene intacto el modo
/// formulario.
///
/// Las devoluciones se construyen en cada render en vez de guardarse: son objetos de dos campos y
/// el diff de Blazor ya reconcilia los manejadores por su identificador, así que memorizarlas solo
/// añadiría estado que mantener sincronizado con los parámetros.
/// </summary>
public abstract class AccountFormComponentBase : ComponentBase
{
    /// <summary>
    /// Error que ha producido el propio componente en modo interactivo (hoy, solo el de las dos
    /// contraseñas que no coinciden). Vive aparte de <see cref="ErrorCode"/> porque aquel lo pone
    /// la página y este no: mezclarlos obligaría a la página a limpiar un error que no escribió.
    /// </summary>
    private string? _ownErrorCode;

    /// <summary>
    /// Código de error de la API. En modo formulario llega por query string, después de que el
    /// manejador del POST redirija; en modo interactivo lo pone la página al fallar la llamada.
    /// En los dos casos entra por el mismo parámetro y se pinta con el mismo marcado.
    /// </summary>
    [Parameter]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Hay un envío interactivo en curso. Solo puede ser true en modo interactivo: en modo
    /// formulario el navegador se va de la página antes de que esto sirviera de nada.
    /// </summary>
    protected bool Submitting { get; private set; }

    /// <summary>
    /// El código que la pantalla tiene que enseñar: el suyo si acaba de fallar una comprobación
    /// propia, y si no el que le pasó la página. Un solo <c>@if</c> en el marcado para los dos.
    /// </summary>
    protected string? ShownErrorCode => _ownErrorCode ?? ErrorCode;

    /// <summary>
    /// La orden de envío que se le pasa a <see cref="AccountForm"/> para un formulario sin campos
    /// —reenviar un código, quitar el teléfono, apagar el segundo factor—.
    /// </summary>
    /// <param name="callback">Lo que puso la página. Sin asignar, se cae al POST de siempre.</param>
    /// <param name="validate">
    /// Comprobación previa que el navegador no puede hacer. Devuelve el código de error a enseñar,
    /// o null si todo está bien.
    /// </param>
    protected EventCallback SubmitFor(EventCallback callback, Func<string?>? validate = null) =>
        callback.HasDelegate
            ? EventCallback.Factory.Create(this, () => RunAsync(() => callback.InvokeAsync(), validate))
            : default;

    /// <inheritdoc cref="SubmitFor(EventCallback, Func{string?})"/>
    /// <param name="payload">
    /// Lo que el usuario escribió, leído en el momento del envío y no antes: en modo interactivo
    /// los campos siguen cambiando hasta que se pulsa el botón.
    /// </param>
    protected EventCallback SubmitFor<TPayload>(
        EventCallback<TPayload> callback,
        Func<TPayload>          payload,
        Func<string?>?          validate = null) =>
        callback.HasDelegate
            ? EventCallback.Factory.Create(this, () => RunAsync(() => callback.InvokeAsync(payload()), validate))
            : default;

    /// <summary>
    /// ¿Las dos casillas de contraseña no dicen lo mismo? Es la única comprobación de las tres
    /// pantallas de contraseña que el navegador no puede hacer con <c>required</c> y
    /// <c>minlength</c>, y por eso es la única que hay escrita en C#.
    ///
    /// Está aquí y no repetida en cada pantalla por lo mismo que el resto de esta clase: es la
    /// misma regla en ChangePassword, SetPassword y ResetPassword, y en modo formulario ya la
    /// aplica una sola función en <c>AccountEndpoints.PasswordsMatch</c>. Lo que cambia entre las
    /// tres es el CÓDIGO de error con el que se rechaza, y ese lo pone cada una, igual que hace
    /// cada manejador del POST.
    /// </summary>
    protected static bool PasswordsMismatch(string? newPassword, string? confirmPassword) =>
        string.IsNullOrEmpty(newPassword) ||
        !string.Equals(newPassword, confirmPassword, StringComparison.Ordinal);

    /// <summary>
    /// ¿La contraseña incumple la política que las tres pantallas enseñan en su lista de
    /// requisitos? Longitud, una mayúscula, una minúscula, un dígito y un carácter especial.
    /// </summary>
    /// <remarks>
    /// ESTO SUBIÓ AQUÍ DESDE LA PANTALLA PROPIA DE BizCenterWeb, que sí lo comprobaba antes de
    /// llamar mientras el componente compartido no. Sin ello, el modo interactivo mandaba a la API
    /// una contraseña que ya se sabía mala y esperaba un viaje de ida y vuelta para decir lo mismo.
    ///
    /// LA MINÚSCULA TAMBIÉN CUENTA, y ese detalle es el que se pierde al escribir esto de memoria:
    /// SignupAPI/Program.cs sobreescribe RequiredLength, RequireDigit, RequireUppercase y
    /// RequireNonAlphanumeric, pero NO RequireLowercase, que en Identity vale true por defecto. Sin
    /// esta condición una contraseña como "PASSWORD1" pasaba el filtro del cliente y el servidor la
    /// rechazaba después, sin que la pantalla hubiera dicho nunca que faltaba una minúscula.
    ///
    /// El navegador ya exige <c>required</c> y <c>minlength="8"</c>, así que la longitud se
    /// comprueba dos veces a propósito: en móvil el mismo componente puede montarse donde el
    /// atributo no llegue a aplicarse, y una comprobación de más aquí no cuesta nada.
    ///
    /// EL CARÁCTER ESPECIAL ES LA CONDICIÓN QUE FALTABA, y faltaba de las DOS mitades a la vez.
    /// <c>ValidationPatterns.PasswordPattern</c> —el que aplican los validadores de SignupAPI a
    /// reset, change y set— exige <c>(?=.*[^A-Za-z0-9])</c>, pero ni la lista de requisitos lo
    /// mencionaba ni esta comprobación lo miraba. El usuario cumplía las cuatro líneas que leía y
    /// el servidor le rechazaba la contraseña sin decirle por qué. Se arregla en pareja: la línea
    /// en la lista (<c>ResetPassword.RequirementSpecial</c>, en las tres pantallas) y la condición
    /// aquí. Una sola de las dos vuelve a desalinear lo que se promete de lo que se exige.
    ///
    /// "Especial" es lo mismo que dice el patrón: cualquier carácter que no sea letra ASCII ni
    /// dígito. <see cref="char.IsLetterOrDigit(char)"/> no vale tal cual —acepta letras de
    /// cualquier alfabeto, así que una "ñ" o una "ü" no contarían como especiales aquí y sí para
    /// el servidor—, y por eso la condición se escribe sobre el mismo conjunto ASCII del patrón.
    /// </remarks>
    protected static bool PasswordFailsPolicy(string? password) =>
        string.IsNullOrEmpty(password) ||
        password.Length < 8            ||
        !password.Any(char.IsUpper)    ||
        !password.Any(char.IsLower)    ||
        !password.Any(char.IsDigit)    ||
        !password.Any(IsSpecial);

    /// <summary>
    /// Un carácter que el patrón del servidor cuenta como especial: cualquiera fuera de
    /// <c>A-Z a-z 0-9</c>. Se compara contra ese conjunto ASCII y no con
    /// <see cref="char.IsLetterOrDigit(char)"/> porque aquel acepta letras acentuadas y de otros
    /// alfabetos, y entonces las dos mitades de la política dejarían de decir lo mismo.
    /// </summary>
    private static bool IsSpecial(char c) =>
        c is not (>= 'a' and <= 'z') and not (>= 'A' and <= 'Z') and not (>= '0' and <= '9');

    /// <summary>
    /// El código de error de la contraseña nueva, o null si no hay nada que objetar. Un solo
    /// código para los dos fallos porque el texto que se enseña ya remite a la lista de requisitos
    /// que el usuario tiene justo encima, y porque es lo que hace el manejador del POST.
    /// </summary>
    protected static string? NewPasswordProblem(
        string? newPassword, string? confirmPassword, string errorCode) =>
        PasswordFailsPolicy(newPassword) || PasswordsMismatch(newPassword, confirmPassword)
            ? errorCode
            : null;

    private async Task RunAsync(Func<Task> invoke, Func<string?>? validate)
    {
        // Un segundo envío mientras el primero sigue en el aire duplicaría la operación: dos SMS,
        // dos cambios de contraseña. En modo formulario de esto se encarga la recarga de página.
        if (Submitting) return;

        // Se reevalúa en cada envío, así que un fallo propio desaparece en cuanto se corrige.
        _ownErrorCode = validate?.Invoke();
        if (!string.IsNullOrWhiteSpace(_ownErrorCode)) return;

        Submitting = true;
        try
        {
            await invoke();
        }
        finally
        {
            // En finally y no al final: si la página deja escapar una excepción, el formulario
            // tiene que quedar utilizable para reintentar en vez de bloqueado para siempre.
            Submitting = false;
        }
    }
}
