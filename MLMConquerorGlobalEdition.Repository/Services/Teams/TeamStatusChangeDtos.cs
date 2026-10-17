using System.Text.Json.Serialization;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <summary>
/// Una fila del reporte de cambios de estado: estado ACTUAL de la cuenta y de la membresía, con la
/// fecha de su último cambio, y si está calificado ahora (IQualificationService, corrección de Nick
/// N3; sin «Calificado desde»). Las columnas de MemberProfiles se buscan, filtran y ordenan en la
/// base; el resto se rellena por página.
/// </summary>
public sealed class TeamStatusChangeView
{
    public string    MemberId                  { get; set; } = string.Empty;
    public string    FirstName                 { get; set; } = string.Empty;
    public string    LastName                  { get; set; } = string.Empty;
    public DateTime  EnrollDate                { get; set; }
    public string    AccountStatus             { get; set; } = string.Empty;
    public DateTime? AccountStatusChangedAt    { get; set; }
    public string    MembershipStatus          { get; set; } = "None";
    /// <summary>Nivel más alto de los productos de la orden de la suscripción actual (N2).</summary>
    public string?   MembershipLevelName       { get; set; }
    public DateTime? MembershipStatusChangedAt { get; set; }
    public bool      IsQualified               { get; set; }
    public string?   SponsorMemberId           { get; set; }
    public string?   SponsorFullName           { get; set; }
    public string?   DualUplineMemberId        { get; set; }
    public string?   DualUplineFullName        { get; set; }
    /// <summary>"Left" o "Right"; solo en la vista Dual Team.</summary>
    public string?   Leg                       { get; set; }

    /// <summary>Ruta binaria, solo para calcular la pierna en el servidor. Nunca sale (se anula y no se serializa).</summary>
    [JsonIgnore]
    public string?   DualPath                  { get; set; }
}
