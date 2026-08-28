namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

public class ConfirmEmailRequest
{
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Token de Identity codificado en base64url. Viaja codificado porque el token crudo lleva
    /// '+', '/' y '=' — caracteres que una query string corrompe.
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
