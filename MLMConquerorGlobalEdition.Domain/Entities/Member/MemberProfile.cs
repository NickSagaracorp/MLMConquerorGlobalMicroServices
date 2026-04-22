using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Loyalty;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Interfaces;

namespace MLMConquerorGlobalEdition.Domain.Entities.Member;

public class MemberProfile : AuditChangesStringKey, IAuditable
{
    // Identity
    public Guid UserId { get; set; }
    public string MemberId { get; set; } = string.Empty;     // Human-readable: AMB-000001
    public string Email { get; set; } = string.Empty;        // Stored here for reference before Identity user is activated

    // Personal info
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }

    // Tax identity — stored encrypted; required for US residents
    public string? SsnEncrypted { get; set; }

    // Business identity
    public string? BusinessName { get; set; }
    public bool ShowBusinessName { get; set; }

    // MLM-specific
    public MemberType MemberType { get; set; }
    public MemberAccountStatus Status { get; set; } = MemberAccountStatus.Active;
    public DateTime EnrollDate { get; set; }
    public string? SponsorMemberId { get; set; }
    public string? ReplicateSiteSlug { get; set; }
    public string? ProfilePhotoUrl { get; set; }

    // Privacy settings
    public bool IsNamePublic { get; set; }
    public bool IsEmailPublic { get; set; }
    public bool IsPhonePublic { get; set; }

    // Navigation
    public MembershipSubscription? ActiveMembership { get; set; }
    public ICollection<MembershipSubscription> MembershipHistory { get; set; } = new List<MembershipSubscription>();
    public GenealogyEntity? EnrollmentNode { get; set; }
    public DualTeamEntity? BinaryNode { get; set; }
    public ICollection<TokenBalance> TokenBalances { get; set; } = new List<TokenBalance>();
    public ICollection<LoyaltyPoints> LoyaltyPoints { get; set; } = new List<LoyaltyPoints>();
    public ICollection<MemberStatusHistory> StatusHistory { get; set; } = new List<MemberStatusHistory>();
    public ICollection<MemberProfileNotificationTracking> Notifications { get; set; } = new List<MemberProfileNotificationTracking>();
}
