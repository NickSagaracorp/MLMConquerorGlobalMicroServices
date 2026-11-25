namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;

public class PromoMemberDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Correo y estado de la última suscripción: la página de miembros de la promo ya tenía estas columnas pero la
    // API no las mandaba y salían vacías. Las llena el grid de servidor (GetPromoMembersGrid).
    public string Email { get; set; } = string.Empty;
    public string MembershipLevel { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTime SignupDate { get; set; }
}
