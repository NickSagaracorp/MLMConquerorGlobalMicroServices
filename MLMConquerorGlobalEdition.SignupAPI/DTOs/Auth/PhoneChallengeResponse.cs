namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Lo que devuelve el alta del teléfono: el challenge que hay que redimir en
/// <c>POST /api/v1/auth/phone/verify</c> y el destino enmascarado, para que la pantalla pueda
/// decir a qué número fue el código sin enseñarlo entero.
/// </summary>
public class PhoneChallengeResponse
{
    public string   ChallengeToken { get; set; } = string.Empty;
    public string   MaskedTarget   { get; set; } = string.Empty;
    public DateTime ExpiresAt      { get; set; }
}
