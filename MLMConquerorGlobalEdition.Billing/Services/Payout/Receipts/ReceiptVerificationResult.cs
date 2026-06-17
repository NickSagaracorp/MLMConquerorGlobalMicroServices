namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public record ReceiptVerificationResult(
    bool HasReceipt,
    bool HashMatches,
    bool ChainValid,
    bool Anchored,
    string? AnchorRef,
    string Detail);
