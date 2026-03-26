namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;

public class ProfileDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? BusinessName { get; set; }
    public string? PhotoUrl { get; set; }
    public string MemberType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EnrollDate { get; set; }
    public string? SponsorMemberId { get; set; }
}
