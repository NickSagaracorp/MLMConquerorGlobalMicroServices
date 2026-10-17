namespace MLMConquerorGlobalEdition.SharedKernel.Billing;

/// <summary>
/// Reglas de la cuota anual del Biz Center (Plan K). Las aplican Billing (para cobrar) y el BizCenter
/// (para enseñar si se puede pagar); viven aquí para que no diverjan.
/// </summary>
public static class AnnualFeeRules
{
    /// <summary>"Annual: Biz Center" (HasData, mismo id en todos los entornos). Se gasta primero.</summary>
    public const int AnnualBizCenterTokenTypeId = 23;

    /// <summary>"Annual Fee" (HasData). Se gasta si no hay del 23.</summary>
    public const int AnnualFeeTokenTypeId = 10;

    /// <summary>
    /// Los tokens que pagan la cuota, en orden de preferencia (K8). No el 24 "Legacy Biz Center Fee"
    /// (no otorga producto) ni el 103 (token del plan que emite el camino de Travel Advantage).
    /// </summary>
    public static readonly IReadOnlyList<int> FeeTokenTypeIds = [AnnualBizCenterTokenTypeId, AnnualFeeTokenTypeId];

    /// <summary>"Pagar ahora" vale desde estos días antes del vencimiento (K14).</summary>
    public const int PayAheadDays = 30;

    /// <summary>"Cobrada hace poco": el último cobro fue hace menos de estos meses (K5).</summary>
    public const int RecentlyBilledMonths = 11;

    /// <summary>¿Cobra el motor la cuota en el aniversario? Apagado por defecto (K13). Se lee en cada llamada.</summary>
    public const string AutoChargeEnabledConfigKey = "Billing:AnnualFee:AutoChargeEnabled";

    /// <summary>Minutos que un pago de cuota puede seguir Pending antes de pasar a conciliar (K11).</summary>
    public const string StalePendingMinutesConfigKey = "Billing:AnnualFee:StalePendingMinutes";

    public const int DefaultStalePendingMinutes = 30;

    /// <summary>
    /// ¿Programa Billing el alta diaria de estados de cuota? Apagado por defecto: el trabajo se registra
    /// con Cron.Never() y solo corre si alguien lo lanza a mano desde Hangfire (Plan K, Tarea 8).
    /// </summary>
    public const string EnrollmentJobEnabledConfigKey = "Billing:AnnualFee:EnrollmentJobEnabled";

    /// <summary>Estados de cuota que el trabajo de alta crea por guardado (K3).</summary>
    public const int EnrollmentBatchSize = 1000;

    /// <summary>Tope de lotes por ejecución del trabajo de alta: 500.000 estados.</summary>
    public const int EnrollmentMaxBatchesPerRun = 500;
}

/// <summary>Con qué se paga la cuota. Sin tarjeta hasta el Plan I. Coinciden con el enum de dominio.</summary>
public static class AnnualFeeMethods
{
    public const string Token             = "Token";
    public const string CommissionBalance = "CommissionBalance";
    public const string EWallet           = "EWallet";

    public static readonly IReadOnlyList<string> All = [Token, CommissionBalance, EWallet];

    /// <summary>
    /// El nombre canónico, o null si no es un método de la cuota (la tarjeta no lo es). Solo por nombre:
    /// "1" no vale, igual que en la compra de tokens.
    /// </summary>
    public static string? Normalize(string? value) =>
        All.FirstOrDefault(m => string.Equals(m, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Cómo está la cuota de un miembro, para la pantalla.</summary>
public static class AnnualFeeStatuses
{
    public const string NotApplicable = "NotApplicable";
    public const string UpToDate      = "UpToDate";
    public const string DueSoon       = "DueSoon";
    public const string Due           = "Due";
    public const string UnderReview   = "UnderReview";
    public const string Waived        = "Waived";
}

/// <summary>Estados de un pago de cuota tal y como viajan (nombres del enum de dominio).</summary>
public static class AnnualFeePaymentStatuses
{
    public const string Pending             = "Pending";
    public const string Completed           = "Completed";
    public const string Failed              = "Failed";
    public const string NeedsReconciliation = "NeedsReconciliation";
}

/// <summary>Códigos de error de la cuota anual. La pantalla los traduce con AnnualFeeMessages.</summary>
public static class AnnualFeeErrors
{
    public const string NotAMember                    = TokenPurchaseErrors.NotAMember;
    public const string Impersonation                 = "IMPERSONATION_CANNOT_PAY_ANNUAL_FEE";
    public const string MethodNotSupported            = "ANNUAL_FEE_METHOD_NOT_SUPPORTED";
    public const string IdempotencyKeyRequired        = TokenPurchaseErrors.IdempotencyKeyRequired;
    public const string IdempotencyKeyReused          = TokenPurchaseErrors.IdempotencyKeyReused;
    public const string NotApplicable                 = "ANNUAL_FEE_NOT_APPLICABLE";
    public const string PlanNotConfigured             = "ANNUAL_FEE_PLAN_NOT_CONFIGURED";
    public const string PriceNotSet                   = "ANNUAL_FEE_PRICE_NOT_SET";
    public const string StateNotFound                 = "ANNUAL_FEE_STATE_NOT_FOUND";
    public const string AccountNotActive              = "ANNUAL_FEE_ACCOUNT_NOT_ACTIVE";

    /// <summary>La membresía vigente está en HoldByBilling: no hay cuota que cobrar (Nick, 2026-10-17).</summary>
    public const string MembershipOnHold              = "ANNUAL_FEE_MEMBERSHIP_ON_HOLD";
    public const string Waived                        = "ANNUAL_FEE_WAIVED";
    public const string NotDue                        = "ANNUAL_FEE_NOT_DUE";
    public const string AlreadyPaid                   = "ANNUAL_FEE_ALREADY_PAID";
    public const string InProgress                    = "ANNUAL_FEE_IN_PROGRESS";
    public const string NeedsReview                   = "ANNUAL_FEE_NEEDS_REVIEW";
    public const string NoFeeToken                    = "ANNUAL_FEE_NO_TOKEN";
    public const string NoFunds                       = "ANNUAL_FEE_NO_FUNDS";
    public const string Busy                          = "ANNUAL_FEE_BUSY";
    public const string AutoChargeDisabled            = "ANNUAL_FEE_AUTO_CHARGE_DISABLED";
    public const string PaymentNotFound               = "ANNUAL_FEE_PAYMENT_NOT_FOUND";
    public const string NotUnderReview                = "ANNUAL_FEE_PAYMENT_NOT_UNDER_REVIEW";
    public const string NotCharged                    = "ANNUAL_FEE_NOT_CHARGED";
    public const string InsufficientCommissionBalance = TokenPurchaseErrors.InsufficientCommissionBalance;
    public const string InsufficientEWalletBalance    = TokenPurchaseErrors.InsufficientEWalletBalance;
    public const string EWalletNotFound               = TokenPurchaseErrors.EWalletNotFound;
    public const string EWalletUnavailable            = TokenPurchaseErrors.EWalletUnavailable;
    public const string EWalletRejected               = TokenPurchaseErrors.EWalletRejected;
}

/// <summary>
/// "Pagar ahora". NO lleva MemberId: el miembro sale del token en el BizCenter y otra vez en Billing.
/// </summary>
public sealed class PayAnnualFeeRequest
{
    public string Method         { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class AnnualFeePaymentResultDto
{
    public string   PaymentId    { get; set; } = string.Empty;
    public string   OrderId      { get; set; } = string.Empty;
    public string?  OrderNo      { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public string   Method       { get; set; } = string.Empty;
    public decimal  Amount       { get; set; }
    public DateTime CycleDueDate { get; set; }
    public DateTime NextDueDate  { get; set; }
    public string?  TokenCode    { get; set; }
}

/// <summary>Lo que enseña la tarjeta de la cuota (BizCenter y Admin).</summary>
public sealed class AnnualFeeOverviewDto
{
    public bool      Applies            { get; set; }
    public string    Status             { get; set; } = AnnualFeeStatuses.NotApplicable;
    public decimal?  Amount             { get; set; }
    public DateTime? NextDueDate        { get; set; }
    public DateTime? LastPaidDate       { get; set; }
    public bool      CanPayNow          { get; set; }

    /// <summary>Código de AnnualFeeErrors si no se puede pagar ahora.</summary>
    public string?   NotPayableReason   { get; set; }

    public int       AvailableFeeTokens { get; set; }
    public bool      FeesFromBalance    { get; set; }
    public List<AnnualFeePaymentRowDto> RecentPayments { get; set; } = new();
}

public sealed class AnnualFeePaymentRowDto
{
    public string   PaymentId    { get; set; } = string.Empty;
    public DateTime Date         { get; set; }
    public DateTime CycleDueDate { get; set; }
    public string   Method       { get; set; } = string.Empty;
    public string   Status       { get; set; } = string.Empty;
    public decimal  Amount       { get; set; }
}

public sealed class AnnualFeePreferenceRequest
{
    public bool FeesFromBalance { get; set; }
}

/// <summary>Tesorería cierra un pago de cuota a conciliar tras mirar i-Payout.</summary>
public sealed class ReconcileAnnualFeePaymentRequest
{
    /// <summary>True: el dinero salió; se completa y avanza el ciclo. False: no salió; se da por fallido.</summary>
    public bool    Charged          { get; set; }
    public string? GatewayReference { get; set; }
    public string? Notes            { get; set; }
}
