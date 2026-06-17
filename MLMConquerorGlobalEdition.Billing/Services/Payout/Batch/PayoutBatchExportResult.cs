namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Batch;

public record PayoutBatchExportResult(string BatchId, int MemberCount, decimal TotalAmountUsd, byte[] CsvBytes, string FileName);
