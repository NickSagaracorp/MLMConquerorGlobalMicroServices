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

    /// <summary>Plan Q (DQ8): «nombre apellido», calculada y persistida (CI_AI), para buscar por prefijo con indice.</summary>
    public string? SearchName { get; private set; }
    /// <summary>Plan Q (DQ8): «apellido nombre», para encontrar por el apellido.</summary>
    public string? SearchNameReversed { get; private set; }
    public DateTime DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }

    // Tax identity — stored encrypted; required for US residents (SSN for individuals,
    // EIN for businesses). The plaintext is never returned to the client; only the
    // last 4 digits are decrypted for display.
    public string? SsnEncrypted { get; set; }
    public string? EinEncrypted { get; set; }

    // Business identity
    public string? BusinessName { get; set; }
    public bool ShowBusinessName { get; set; }

    // MLM-specific
    public MemberType MemberType { get; set; }
    public MemberAccountStatus Status { get; set; } = MemberAccountStatus.Active;
    public DateTime EnrollDate { get; set; }
    public string? SponsorMemberId { get; set; }
    public string? ReplicateSiteSlug { get; set; }

    /// <summary>
    /// Plan Q (DQ10): el nivel del alta (regla N2 del Plan J: el nivel mas alto del primer pedido con productos de membresia).
    /// Lo mantiene SignupMembershipLevel.RefreshAsync; null si el miembro no tiene pedido de membresia.
    /// </summary>
    public int? SignupMembershipLevelId { get; set; }

    /// <summary>
    /// Marca de mantener calificado (Plan J, corrección de Nick N3): con la membresía activa, cuenta
    /// como calificado aunque se inscribiera hace más de 12 meses y no haya pagado la anualidad.
    /// Por defecto false. Editarla desde el Admin queda fuera del Plan J.
    /// </summary>
    public bool KeepQualified { get; set; }

    /// <summary>
    /// Preferencia de pagar las cuotas con el saldo (Plan K, K7; confirmada por Nick). Encendida: si no
    /// hay un token de cuota, el cobro automático usa las comisiones y después la eWallet. Apagada: solo
    /// tokens de cuota que el miembro ya tenga. Equivale a la DailyTokenDeduction del legado ("usar
    /// comisiones para pagar"), que en la cuota anual el legado ni consultaba; aquí sí se respeta.
    /// </summary>
    public bool FeesFromBalance { get; set; } = true;
    public string? ProfilePhotoUrl { get; set; }

    /// <summary>
    /// ISO 639-1 code (en, es, pt, fr…) — governs the BizCenter UI language.
    /// Defaults to "en" so existing rows are unaffected.
    /// </summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>How often commissions are pushed to the member's preferred wallet.</summary>
    public PayoutFrequency PayoutFrequency { get; set; } = PayoutFrequency.Weekly;

    // Privacy settings — controls what the member's public/replicate page shows
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
