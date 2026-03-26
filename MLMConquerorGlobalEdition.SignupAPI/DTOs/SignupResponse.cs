namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public class SignupResponse
{
    /// <summary>The OrderId that serves as the signupId for the multi-step wizard flow.</summary>
    public string SignupId { get; set; } = string.Empty;

    public string MemberId     { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string MemberType   { get; set; } = string.Empty;
    public DateTime EnrollDate { get; set; }

    // ── Auth tokens returned after the Complete step ───────────────────────
    public string? AccessToken   { get; set; }
    public string? RefreshToken  { get; set; }
    public DateTime? TokenExpiry { get; set; }
}
