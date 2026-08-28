namespace MLMConquerorGlobalEdition.Authn.Models;

/// <param name="SharedKey">El secreto en base32, para entrada manual si no se puede escanear.</param>
/// <param name="AuthenticatorUri">URI otpauth:// que codifica el QR.</param>
/// <param name="QrCodePngDataUri">PNG en data-URI, listo para un &lt;img src&gt;.</param>
public sealed record TotpEnrollment(string SharedKey, string AuthenticatorUri, string QrCodePngDataUri);
