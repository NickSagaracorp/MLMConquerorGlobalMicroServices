using Microsoft.AspNetCore.Mvc;

namespace MLMConquerorGlobalEdition.SharedKernel.Server.Security;

/// <summary>
/// Exige un permiso de step-up vigente y sin usar para <paramref name="operationKey"/> en la
/// cabecera <c>X-Step-Up-Token</c>. Ver <see cref="StepUpAuthorizationFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresStepUpAttribute : TypeFilterAttribute
{
    public RequiresStepUpAttribute(string operationKey) : base(typeof(StepUpAuthorizationFilter))
    {
        OperationKey = operationKey;
        Arguments    = new object[] { operationKey };
    }

    public string OperationKey { get; }
}
