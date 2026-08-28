using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries;

/// <summary>
/// Enmascarado del teléfono para las consultas de cuenta.
/// </summary>
/// <remarks>
/// Se construye desde <see cref="ApplicationUser.TwoFactorPhoneLast4"/>, que se guarda en claro
/// justo para esto. Nunca se descifra <c>TwoFactorPhoneEncrypted</c>: ese número es a la vez PII y
/// factor de autenticación, y sacarlo entero lo dejaría en el cuerpo de la respuesta y en
/// cualquier registro intermedio a cambio de nada, porque la pantalla solo enseña cuatro dígitos.
///
/// El prefijo es de longitud fija. La alternativa —tantos asteriscos como dígitos tenga el
/// número— exigiría descifrarlo para contarlos, que es precisamente lo que se está evitando; y de
/// paso filtraría la longitud, que en E.164 acota el país.
/// </remarks>
public static class AccountMasking
{
    private const string Prefix = "********";

    /// <summary>Devuelve null cuando la cuenta no tiene teléfono dado de alta.</summary>
    public static string? MaskPhoneFromLast4(string? last4) =>
        string.IsNullOrWhiteSpace(last4) ? null : Prefix + last4.Trim();
}
