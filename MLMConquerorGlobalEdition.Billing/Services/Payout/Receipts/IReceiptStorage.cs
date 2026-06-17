namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public interface IReceiptStorage
{
    Task<string> SaveAsync(string fileName, byte[] content, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(string fileName, CancellationToken ct = default);
}
