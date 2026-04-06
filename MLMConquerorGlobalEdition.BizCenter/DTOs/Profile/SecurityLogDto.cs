namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;

public class SecurityLogDto
{
    public string   EventType  { get; set; } = string.Empty;
    public string   IpAddress  { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string   Status     { get; set; } = string.Empty;
}
