namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Fija la primera contraseña de una cuenta que no tiene ninguna. No lleva contraseña actual
/// —no existe—; para cambiar una que ya está puesta se usa <c>PUT /api/v1/auth/change-password</c>.
/// </summary>
public class SetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
