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

/// <summary>El cobro de la cuota anual del Biz Center (Plan K).</summary>
public interface IAnnualFeeChargeService
{
    /// <summary>
    /// El cobro automático de un estado de cuota anual. Lo llama RecurringBillingProcessor cuando le
    /// llega un estado con Purpose = AnnualFee. <paramref name="force"/>: el bill-now de soporte.
    /// </summary>
    Task<Result<AnnualFeeChargeOutcome>> ChargeDueAsync(string billingStateId, bool force, CancellationToken ct = default);
}
