namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;

public class UpdateProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
    public string? BusinessName { get; set; }
    public bool ShowBusinessName { get; set; }
}
