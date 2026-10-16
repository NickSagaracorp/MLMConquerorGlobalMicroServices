using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs;

public class SignupResponse
{
    /// <summary>The OrderId that serves as the signupId for the multi-step wizard flow.</summary>
    public string SignupId { get; set; } = string.Empty;

    public string MemberId     { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string MemberType   { get; set; } = string.Empty;
    public DateTime EnrollDate { get; set; }

    public string? AccessToken   { get; set; }
    public string? RefreshToken  { get; set; }
    public DateTime? TokenExpiry { get; set; }

    /// <summary>
    /// Solo con PaymentMethod = Entrax: el QR que hay que pagar. Sin tokens: el alta se activa en el
    /// servidor cuando Entrax confirma el cobro, y la persona inicia sesión después.
    /// </summary>
    public EntraxSignupPaymentDto? Entrax { get; set; }
}
