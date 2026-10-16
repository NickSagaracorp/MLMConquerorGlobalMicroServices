using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SharedKernel.Server.Security;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

/// <summary>El permiso de step-up en Billing: validarlo y, como paso aparte, quemarlo con la marca de Billing.</summary>
public interface IStepUpGrantCheck
{
    /// <summary>
    /// El jti si el permiso vale para este usuario y esta operación (firma, vigencia, usuario,
    /// operación); null si no. No lo quema.
    /// </summary>
    string? ValidJti(string? grant, string userId, string operationKey);

    /// <summary>
    /// Quema el permiso para Billing: true la primera vez, false si ya se había usado aquí. NO degrada:
    /// si la caché no responde, lanza, y quien llama tiene que rechazar (fallar cerrado).
    /// </summary>
    Task<bool> TryConsumeAsync(string jti, CancellationToken ct = default);
}

/// <remarks>
/// El BizCenter valida el permiso y lo quema con su marca (StepUpAuthorizationFilter) antes de reenviarlo.
/// Billing lo vuelve a validar y lo quema con OTRA marca (<see cref="CacheKeys.BillingStepUpGrantConsumed"/>):
/// con la misma, la llamada que el BizCenter reenvía chocaría con su propia marca; sin quemarlo, el permiso
/// se podría reutilizar contra Billing directamente mientras vive (~2 min). Así vale una vez en cada
/// servicio. Validar y quemar van separados porque el controlador decide cuándo quemar (ver
/// TokenPurchasesController: una repetición de una clave que ya tiene compra no quema).
///
/// La marca usa <see cref="ICacheService.TryClaimOnceAsync"/>: SET NX en Redis cuando el anfitrión
/// registra IConnectionMultiplexer (Program.cs lo hace), atómico entre instancias. Dura más que el
/// permiso para que no reaparezca "sin usar" antes de vencer.
/// </remarks>
public sealed class StepUpGrantCheck : IStepUpGrantCheck
{
    private readonly StepUpGrantValidator _validator;
    private readonly ICacheService        _cache;

    public StepUpGrantCheck(StepUpGrantValidator validator, ICacheService cache)
    {
        _validator = validator;
        _cache     = cache;
    }

    public string? ValidJti(string? grant, string userId, string operationKey)
    {
        if (string.IsNullOrWhiteSpace(grant) || string.IsNullOrWhiteSpace(userId)) return null;
        var result = _validator.Validate(grant, userId, operationKey);
        return result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value) ? result.Value : null;
    }

    public Task<bool> TryConsumeAsync(string jti, CancellationToken ct = default) =>
        _cache.TryClaimOnceAsync(
            CacheKeys.BillingStepUpGrantConsumed(jti),
            StepUpGrant.Lifetime + TimeSpan.FromMinutes(1),
            ct);
}
