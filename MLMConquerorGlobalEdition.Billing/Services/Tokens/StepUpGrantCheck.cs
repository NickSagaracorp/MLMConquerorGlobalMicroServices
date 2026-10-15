using MLMConquerorGlobalEdition.SharedKernel.Server.Security;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>¿Este permiso de step-up vale para este usuario y esta operación? Sin estado: no lo quema.</summary>
public interface IStepUpGrantCheck
{
    bool IsValid(string? grant, string userId, string operationKey);
}

/// <remarks>
/// El BizCenter valida el permiso y lo marca como usado en la caché compartida (StepUpAuthorizationFilter).
/// Billing solo comprueba firma, usuario, operación y vigencia: si además lo quemara, la misma llamada
/// que el BizCenter reenvía chocaría con su propia marca. Validarlo aquí cierra la puerta de pagar con
/// saldo llamando a Billing directamente con el JWT (D13).
/// </remarks>
public sealed class StepUpGrantCheck : IStepUpGrantCheck
{
    private readonly StepUpGrantValidator _validator;

    public StepUpGrantCheck(StepUpGrantValidator validator) => _validator = validator;

    public bool IsValid(string? grant, string userId, string operationKey) =>
        !string.IsNullOrWhiteSpace(grant)
        && !string.IsNullOrWhiteSpace(userId)
        && _validator.Validate(grant, userId, operationKey).IsSuccess;
}
