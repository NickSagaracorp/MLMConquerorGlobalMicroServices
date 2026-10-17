using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.AnnualFee;

/// <summary>Resultados del cobro automático, con los mismos nombres que RecurringBillingProcessorResult.Outcome.</summary>
public static class AnnualFeeOutcomes
{
    public const string Success = "Success";
    public const string Failed  = "Failed";
    public const string Skipped = "Skipped";
}

/// <summary>Lo que devuelve el cobro automático al motor.</summary>
public sealed record AnnualFeeChargeOutcome(
    string  Outcome,
    string? FundingSource    = null,
    string? OrderId          = null,
    string? PaymentHistoryId = null,
    string? FailureReason    = null);

/// <summary>Quién paga (siempre del token), con qué identidad queda el rastro y qué pide.</summary>
public sealed record AnnualFeePayNowInput(string MemberId, string Actor, MLMConquerorGlobalEdition.SharedKernel.Billing.PayAnnualFeeRequest Request);

/// <summary>El cobro de la cuota anual del Biz Center (Plan K).</summary>
public interface IAnnualFeeChargeService
{
    /// <summary>
    /// El cobro automático de un estado de cuota anual. Lo llama RecurringBillingProcessor cuando le
    /// llega un estado con Purpose = AnnualFee. <paramref name="force"/>: el bill-now de soporte.
    /// </summary>
    Task<Result<AnnualFeeChargeOutcome>> ChargeDueAsync(string billingStateId, bool force, CancellationToken ct = default);

    /// <summary>"Pagar ahora" del miembro del token (K14). Misma clave = mismo resultado (K10).</summary>
    Task<Result<MLMConquerorGlobalEdition.SharedKernel.Billing.AnnualFeePaymentResultDto>> PayNowAsync(
        AnnualFeePayNowInput input, CancellationToken ct = default);

    /// <summary>
    /// ¿Esta clave ya tiene un pago de este miembro, en el estado que sea? Con ella, PayNowAsync solo
    /// devuelve lo que ya pasó: el controlador no quema un permiso de step-up en una repetición.
    /// </summary>
    Task<bool> HasPaymentAsync(string memberId, string? idempotencyKey, CancellationToken ct = default);
}
