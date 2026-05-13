using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Billing;

/// <summary>
/// Defines the ordered fallback chain for a given (OperationType, PrimaryProcessor) pair.
/// StepOrder=1 is the first fallback after the primary fails.
/// </summary>
public class GatewayFallbackRule : AuditChangesIntKey
{
    public BillingOperationType OperationType     { get; set; }
    public CardProcessor        PrimaryProcessor  { get; set; }
    public int                  StepOrder         { get; set; }
    public CardProcessor        NextProcessor     { get; set; }

    /// <summary>
    /// Minutes to wait before executing this fallback step.
    /// 0 = immediate; 60 = used for recurring USA/Canada delayed retry.
    /// </summary>
    public int  DelayMinutes      { get; set; }

    /// <summary>
    /// When true the charge is submitted in USD regardless of the presentment
    /// currency selected for the primary attempt.
    /// False = Stripe steps which keep the presented currency.
    /// </summary>
    public bool ForceUsdOnFallback { get; set; }
}
