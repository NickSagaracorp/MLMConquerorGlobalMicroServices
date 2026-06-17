namespace MLMConquerorGlobalEdition.Billing.Services.Payout.Receipts;

public interface IReceiptPdfRenderer
{
    /// <summary>Renders a payout receipt PDF and returns the bytes.</summary>
    byte[] Render(PayoutReceiptData data);
}
