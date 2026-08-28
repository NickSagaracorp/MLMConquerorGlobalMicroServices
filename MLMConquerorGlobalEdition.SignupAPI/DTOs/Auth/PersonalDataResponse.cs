using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

/// <summary>
/// Los datos que el sistema guarda de la cuenta, para que el usuario los consulte o los descargue.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lista explícita, nunca por exclusión.</b> Cada campo que sale de aquí está escrito a mano en
/// <c>GetPersonalDataHandler</c>. No se serializa <c>ApplicationUser</c> ni <c>MemberProfile</c>
/// para después quitar lo que sobra: con lista de exclusión, la columna sensible que alguien añada
/// mañana a cualquiera de las dos entidades aparecería sola en esta respuesta sin que nadie tocara
/// este archivo. Con lista explícita no aparece hasta que alguien la escriba aquí a conciencia.
/// </para>
/// <para>
/// <b>Lo que nunca puede salir por aquí</b>, y por eso no tiene propiedad en este DTO: el hash de
/// la contraseña, el token de refresco (ni su hash) y su caducidad, la clave del autenticador de
/// <c>AspNetUserTokens</c>, el teléfono cifrado, el <c>SecurityStamp</c>, el <c>ConcurrencyStamp</c>
/// y las columnas cifradas de identidad fiscal del perfil (SSN/EIN). Son material del sistema, no
/// datos del usuario. Un endpoint de "descarga tus datos" que los incluyera se convertiría en una
/// vía legítima para extraer con qué suplantar la cuenta — y desde una sesión ya autenticada, así
/// que no dispararía ninguna alarma.
/// </para>
/// </remarks>
public class PersonalDataResponse
{
    // ── Cuenta (ApplicationUser) ──────────────────────────────────────────────
    public string  UserId          { get; set; } = string.Empty;
    public string? UserName        { get; set; }
    public string? Email           { get; set; }
    public bool    EmailConfirmed  { get; set; }

    /// <summary>
    /// Teléfono del 2FA, solo enmascarado. El número entero está cifrado y no sale de aquí:
    /// es un factor de autenticación, no un dato de contacto más.
    /// </summary>
    public string? MaskedTwoFactorPhone { get; set; }

    public bool             TwoFactorPhoneConfirmed   { get; set; }
    public bool             TwoFactorEnabled          { get; set; }
    public TwoFactorChannel PreferredTwoFactorChannel { get; set; }
    public DateTime?        TwoFactorEnrolledAt       { get; set; }

    public bool      IsActive        { get; set; }
    public DateTime  CreationDate    { get; set; }
    public string    CreatedBy       { get; set; } = string.Empty;
    public DateTime? LastLoginAt     { get; set; }
    public string?   MemberProfileId { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Null para el personal interno: los usuarios de staff no tienen perfil de miembro.</summary>
    public PersonalDataMemberProfile? MemberProfile { get; set; }
}

/// <summary>
/// Los datos del perfil de miembro. Lista explícita, por el mismo motivo que
/// <see cref="PersonalDataResponse"/>.
/// </summary>
/// <remarks>
/// Sin <c>SsnEncrypted</c> ni <c>EinEncrypted</c>: la identidad fiscal se guarda cifrada y en el
/// resto del sistema solo se descifran sus cuatro últimos dígitos para enseñarlos. Devolverla
/// entera aquí sería exactamente la exposición que ese cifrado evita. Tampoco va <c>RowVersion</c>,
/// que es el testigo de concurrencia de la fila, no un dato del usuario.
///
/// <c>Phone</c> y <c>WhatsApp</c> sí van en claro: son los datos de contacto que el propio miembro
/// escribió, se guardan sin cifrar y su propia pantalla de perfil ya los enseña. El que se
/// enmascara es el del segundo factor, que es otra cosa.
/// </remarks>
public class PersonalDataMemberProfile
{
    public string  MemberId    { get; set; } = string.Empty;
    public string  Email       { get; set; } = string.Empty;
    public string  FirstName   { get; set; } = string.Empty;
    public string  LastName    { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }

    public string? Phone    { get; set; }
    public string? WhatsApp { get; set; }

    public string  Country { get; set; } = string.Empty;
    public string? State   { get; set; }
    public string? City    { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }

    public string? BusinessName     { get; set; }
    public bool    ShowBusinessName { get; set; }

    public MemberType           MemberType        { get; set; }
    public MemberAccountStatus  Status            { get; set; }
    public DateTime             EnrollDate        { get; set; }
    public string?              SponsorMemberId   { get; set; }
    public string?              ReplicateSiteSlug { get; set; }
    public string?              ProfilePhotoUrl   { get; set; }
    public string               DefaultLanguage   { get; set; } = "en";
    public PayoutFrequency      PayoutFrequency   { get; set; }

    public bool IsNamePublic  { get; set; }
    public bool IsEmailPublic { get; set; }
    public bool IsPhonePublic { get; set; }

    public DateTime  CreationDate   { get; set; }
    public DateTime  LastUpdateDate { get; set; }
}
