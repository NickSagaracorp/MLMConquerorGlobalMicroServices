namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;

public record BatchReconcileResult(int Succeeded, int Failed, string BatchStatus);
