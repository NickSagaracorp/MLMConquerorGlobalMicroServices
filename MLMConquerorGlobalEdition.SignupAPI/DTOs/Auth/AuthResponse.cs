namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

public class AuthResponse
{
    public string MemberId     { get; set; } = string.Empty;
    public string UserId       { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string MemberType   { get; set; } = string.Empty;
    public string AccessToken  { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];
}
