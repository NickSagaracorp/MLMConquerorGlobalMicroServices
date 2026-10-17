namespace MLMConquerorGlobalEdition.Domain.Enums;

/// <summary>
/// Estado de un pago de cuota anual. Los valores están en el filtro de UX_AnnualFeePayments_LivePerCycle
/// ("[Status] IN (1, 2, 4)"): no se renumeran.
/// </summary>
public enum AnnualFeePaymentStatus
{
    Pending             = 1,
    Completed           = 2,
    Failed              = 3,
    NeedsReconciliation = 4
}

/// <summary>Con qué se pagó la cuota. Sin tarjeta hasta el Plan I.</summary>
public enum AnnualFeePaymentMethod
{
    Token             = 1,
    CommissionBalance = 2,
    EWallet           = 3
}

/// <summary>Quién lo inició: el motor (aniversario) o el miembro ("Pagar ahora").</summary>
public enum AnnualFeePaymentSource
{
    Automatic = 1,
    PayNow    = 2
}
