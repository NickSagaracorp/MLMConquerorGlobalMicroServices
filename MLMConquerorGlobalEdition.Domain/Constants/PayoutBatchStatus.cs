namespace MLMConquerorGlobalEdition.Domain.Constants;

public static class PayoutBatchStatus
{
    public const string Exported            = "Exported";
    public const string Reconciled          = "Reconciled";
    public const string PartiallyReconciled = "PartiallyReconciled";
    public const string Cancelled           = "Cancelled";
}
