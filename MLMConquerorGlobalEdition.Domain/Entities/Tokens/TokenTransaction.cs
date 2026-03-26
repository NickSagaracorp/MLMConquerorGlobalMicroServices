using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenTransaction : AuditChangesLongKey
{
    public string MemberId { get; set; } = string.Empty;
    public int TokenTypeId { get; set; }
    public TokenTransactionType TransactionType { get; set; }
    public int Quantity { get; set; }
    public string? DistributedToMemberId { get; set; }
    public string? UsedByMemberId { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? GeneratedPdfUrl { get; set; }
    public string? ReferenceId { get; set; }
    public string? Notes { get; set; }
}
