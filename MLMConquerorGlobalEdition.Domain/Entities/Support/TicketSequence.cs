namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

/// <summary>
/// Tracks the daily sequential counter used to generate HD-YYYYMMDD-NNNN ticket numbers.
/// One row per calendar day. Incremented atomically in a serializable transaction.
/// </summary>
public class TicketSequence
{
    public DateTime Date { get; set; }
    public int LastSequence { get; set; }
}
