using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Domain.Entities.Support;

public class Ticket : AuditChangesStringKey
{
    public string TicketNumber { get; set; } = string.Empty;      // HD-YYYYMMDD-NNNN
    public string MemberId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public int? AssignedTeamId { get; set; }
    public int CategoryId { get; set; }
    public string? Subcategory { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketChannel Channel { get; set; } = TicketChannel.Portal;
    public EscalationLevel EscalationLevel { get; set; } = EscalationLevel.Tier1;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? MergedIntoTicketId { get; set; }
    public string? Language { get; set; }
    public string? CustomerTier { get; set; }

    // SLA
    public string? SlaPolicyId { get; set; }
    public DateTime? SlaFirstResponseDue { get; set; }
    public DateTime? SlaFirstResponseAt { get; set; }
    public DateTime? SlaResolutionDue { get; set; }
    public bool IsSlaFirstResponseBreached { get; set; }
    public bool IsSlaResolutionBreached { get; set; }
    public bool IsSlaPaused { get; set; }
    public DateTime? SlaPausedAt { get; set; }
    public double SlaPausedMinutes { get; set; }

    // Resolution
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionSummary { get; set; }
    public string? ResolvedByAgentId { get; set; }

    // CSAT
    public int? CsatScore { get; set; }
    public string? CsatComment { get; set; }
    public DateTime? CsatSubmittedAt { get; set; }

    // Auto-close / follow-up
    public bool FollowUpSent { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public TicketCategory? Category { get; set; }
    public SlaPolicy? SlaPolicy { get; set; }
    public SupportTeam? AssignedTeam { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
    public ICollection<SlaBreach> SlaBreaches { get; set; } = new List<SlaBreach>();
}
