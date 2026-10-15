namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>El permiso que va en la cabecera X-Step-Up-Token del reintento.</summary>
public class StepUpGrantResponse
{
    public string StepUpToken { get; set; } = string.Empty;
}
