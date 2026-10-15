using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>
/// Exige un permiso de step-up vigente y sin usar para <paramref name="operationKey"/> en la
/// cabecera <c>X-Step-Up-Token</c>. Ver <see cref="StepUpAuthorizationFilter"/>.
/// </summary>
/// <remarks>
/// Requiere <c>[Authorize]</c> en el endpoint: el permiso se ata al usuario del token de acceso, y
/// sin sesión no hay usuario contra el que compararlo (el filtro rechaza, pero el 401 correcto es
/// el de la autenticación).
///
/// No se combina con <c>[ReadOnlySafe]</c>: una operación que exige step-up es por definición una
/// escritura sensible, y marcarla como segura para una sesión de suplantación de solo lectura
/// dejaría a quien suplanta pedir códigos al miembro para cambiarle la wallet.
///
/// La clave se comprueba contra <see cref="StepUpOperations"/> al construir el atributo: un error
/// de escritura revienta cuando MVC lee los atributos al arrancar, y no queda como un 403 eterno.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresStepUpAttribute : TypeFilterAttribute
{
    public RequiresStepUpAttribute(string operationKey) : base(typeof(StepUpAuthorizationFilter))
    {
        if (!StepUpOperations.IsKnown(operationKey))
            throw new ArgumentException($"Operación de step-up desconocida: {operationKey}.", nameof(operationKey));

        OperationKey = operationKey;
        Arguments    = new object[] { operationKey };
    }

    public string OperationKey { get; }
}
