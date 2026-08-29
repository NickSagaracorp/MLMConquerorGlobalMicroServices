namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Cierra el enrolamiento del autenticador desde una sesión ya iniciada.
/// </summary>
/// <remarks>
/// Solo lleva el código. A diferencia de <see cref="ConfirmEnrollmentRequest"/>, aquí no hay
/// <c>EnrollmentToken</c>: la credencial es el token de acceso del usuario, que ya entró. Son dos
/// modelos de autenticación distintos y por eso son dos peticiones y dos rutas distintas —un
/// endpoint que aceptara cualquiera de los dos sería difícil de razonar y fácil de romper.
/// </remarks>
public class ConfirmAccountEnrollmentRequest
{
    /// <summary>Los seis dígitos que produce la aplicación de autenticación del usuario.</summary>
    public string Code { get; set; } = string.Empty;
}
