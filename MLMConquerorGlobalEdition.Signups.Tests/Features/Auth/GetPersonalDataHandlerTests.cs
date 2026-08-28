using System.Text.Json;
using System.Text.Json.Serialization;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.PersonalData;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Descarga de datos personales. El usuario sale siempre del token: el handler solo conoce un
/// UserId que el controlador saca de las claims, nunca de la query — con el identificador puesto
/// por quien llama, esto descargaría los datos de cualquier cuenta.
/// </summary>
public class GetPersonalDataHandlerTests
{
    private const string UserId    = "user-001";
    private const string Email     = "usuario@dominio.com";
    private const string MemberId  = "AMB-000042";

    // Valores reconocibles: si alguno aparece en el JSON de salida, es que se ha filtrado.
    private const string PasswordHash    = "AQAAAAIAAYagAAAAEHASH-QUE-NO-DEBE-SALIR";
    private const string RefreshToken    = "REFRESH-TOKEN-QUE-NO-DEBE-SALIR";
    private const string SecurityStamp   = "SECURITY-STAMP-QUE-NO-DEBE-SALIR";
    private const string ConcurrencyStmp = "CONCURRENCY-STAMP-QUE-NO-DEBE-SALIR";
    private const string EncryptedPhone  = "ENC:+14155552671";
    private const string EncryptedSsn    = "ENC:123-45-6789";

    private static readonly DateTime Created  = new(2025, 11, 3, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastLogin = new(2026, 8, 20, 17, 45, 0, DateTimeKind.Utc);
    private static readonly DateTime Enrolled  = new(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Birth     = new(1988, 2, 14, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Usuario con todos los campos sensibles poblados a propósito. La prueba de que no salen
    /// solo vale si en la entidad de entrada realmente estaban.
    /// </summary>
    private static ApplicationUser Member(string? memberProfileId = MemberId) => new()
    {
        Id                        = UserId,
        UserName                  = Email,
        Email                     = Email,
        EmailConfirmed            = true,
        IsActive                  = true,
        MemberProfileId           = memberProfileId,
        CreationDate              = Created,
        CreatedBy                 = "signup",
        LastLoginAt               = LastLogin,
        TwoFactorEnabled          = true,
        PreferredTwoFactorChannel = TwoFactorChannel.Sms,
        TwoFactorEnrolledAt       = Enrolled,
        TwoFactorPhoneEncrypted   = EncryptedPhone,
        TwoFactorPhoneLast4       = "2671",
        TwoFactorPhoneConfirmed   = true,

        PasswordHash       = PasswordHash,
        RefreshToken       = RefreshToken,
        RefreshTokenExpiry = LastLogin.AddDays(7),
        SecurityStamp      = SecurityStamp,
        ConcurrencyStamp   = ConcurrencyStmp
    };

    private static async Task<AppDbContext> DbWithProfileAsync()
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId          = MemberId,
            Email             = Email,
            FirstName         = "Ana",
            LastName          = "Ruiz",
            DateOfBirth       = Birth,
            Phone             = "+34600111222",
            WhatsApp          = "+34600111222",
            Country           = "ES",
            State             = "Madrid",
            City              = "Madrid",
            Address           = "Calle Mayor 1",
            ZipCode           = "28013",
            BusinessName      = "Ruiz Global SL",
            ShowBusinessName  = true,
            MemberType        = MemberType.Ambassador,
            Status            = MemberAccountStatus.Active,
            EnrollDate        = Created,
            SponsorMemberId   = "AMB-000001",
            ReplicateSiteSlug = "ana-ruiz",
            ProfilePhotoUrl   = "https://cdn/ana.png",
            DefaultLanguage   = "es",
            PayoutFrequency   = PayoutFrequency.Weekly,
            IsNamePublic      = true,
            CreationDate      = Created,
            LastUpdateDate    = LastLogin,

            // Identidad fiscal cifrada: está en la fila y no puede salir por la descarga.
            SsnEncrypted = EncryptedSsn
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static GetPersonalDataHandler Handler(
        ApplicationUser? user, AppDbContext db, params string[] roles)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
                   .ReturnsAsync(roles.ToList());
        return new GetPersonalDataHandler(userManager.Object, db);
    }

    private static Task<Result<PersonalDataResponse>> Run(
        ApplicationUser? user, AppDbContext db, params string[] roles)
        => Handler(user, db, roles).Handle(new GetPersonalDataQuery(UserId), CancellationToken.None);

    /// <summary>Lo mismo que sirve <c>/personal-data/download</c>: enums como texto y con sangría.</summary>
    private static readonly JsonSerializerOptions FileOptions = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    // ── lo que sí sale ───────────────────────────────────────────────────────

    [Fact]
    public async Task PersonalData_ReturnsAccountFields()
    {
        var db     = await DbWithProfileAsync();
        var result = await Run(Member(), db, "Ambassador");

        result.IsSuccess.Should().BeTrue(because: result.Error);

        var data = result.Value!;
        data.UserId.Should().Be(UserId);
        data.UserName.Should().Be(Email);
        data.Email.Should().Be(Email);
        data.EmailConfirmed.Should().BeTrue();
        data.IsActive.Should().BeTrue();
        data.CreationDate.Should().Be(Created);
        data.CreatedBy.Should().Be("signup");
        data.LastLoginAt.Should().Be(LastLogin);
        data.MemberProfileId.Should().Be(MemberId);
        data.TwoFactorEnabled.Should().BeTrue();
        data.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Sms);
        data.TwoFactorEnrolledAt.Should().Be(Enrolled);
        data.TwoFactorPhoneConfirmed.Should().BeTrue();
        data.Roles.Should().Equal("Ambassador");
    }

    [Fact]
    public async Task PersonalData_ReturnsMemberProfileFields()
    {
        var db     = await DbWithProfileAsync();
        var result = await Run(Member(), db, "Ambassador");

        var profile = result.Value!.MemberProfile;
        profile.Should().NotBeNull();
        profile!.MemberId.Should().Be(MemberId);
        profile.FirstName.Should().Be("Ana");
        profile.LastName.Should().Be("Ruiz");
        profile.DateOfBirth.Should().Be(Birth);
        profile.Country.Should().Be("ES");
        profile.City.Should().Be("Madrid");
        profile.Address.Should().Be("Calle Mayor 1");
        profile.ZipCode.Should().Be("28013");
        profile.BusinessName.Should().Be("Ruiz Global SL");
        profile.MemberType.Should().Be(MemberType.Ambassador);
        profile.Status.Should().Be(MemberAccountStatus.Active);
        profile.SponsorMemberId.Should().Be("AMB-000001");
        profile.ReplicateSiteSlug.Should().Be("ana-ruiz");
        profile.DefaultLanguage.Should().Be("es");
        profile.PayoutFrequency.Should().Be(PayoutFrequency.Weekly);
        profile.IsNamePublic.Should().BeTrue();
    }

    /// <summary>El personal interno no tiene perfil de miembro; la descarga se queda en la cuenta.</summary>
    [Fact]
    public async Task PersonalData_WhenStaffUser_HasNoMemberProfile()
    {
        var db     = await DbWithProfileAsync();
        var result = await Run(Member(memberProfileId: null), db, "Admin");

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.MemberProfile.Should().BeNull();
        result.Value.Roles.Should().Equal("Admin");
    }

    /// <summary>
    /// El teléfono del segundo factor sale solo enmascarado, desde los cuatro últimos dígitos que
    /// se guardan en claro justo para esto. El número entero está cifrado y no se descifra.
    /// </summary>
    [Fact]
    public async Task PersonalData_MasksTwoFactorPhone()
    {
        var db     = await DbWithProfileAsync();
        var result = await Run(Member(), db);

        result.Value!.MaskedTwoFactorPhone.Should().Be("********2671");
    }

    // ── lo que NO puede salir ────────────────────────────────────────────────

    /// <summary>
    /// La prueba que hay que escribir aunque parezca obvia: es la que fallará el día que alguien
    /// cambie la forma de construir la respuesta —por ejemplo, serializando la entidad y quitando
    /// campos en vez de listarlos— y meta de vuelta material con el que suplantar la cuenta.
    ///
    /// Se comprueba sobre el JSON serializado, no sobre las propiedades del DTO: lo que importa es
    /// lo que sale por el cable, y así la prueba pilla también un campo añadido por otra vía.
    /// </summary>
    [Fact]
    public async Task PersonalData_NeverExposesCredentialMaterial()
    {
        var db     = await DbWithProfileAsync();
        var result = await Run(Member(), db, "Ambassador");

        result.IsSuccess.Should().BeTrue(because: result.Error);

        var json = JsonSerializer.Serialize(result.Value!, FileOptions);

        // Ni los valores…
        json.Should().NotContain(PasswordHash);
        json.Should().NotContain(RefreshToken);
        json.Should().NotContain(SecurityStamp);
        json.Should().NotContain(ConcurrencyStmp);
        json.Should().NotContain(EncryptedPhone);
        json.Should().NotContain(EncryptedSsn);

        // …ni los nombres de campo, que delatarían el mismo dato bajo otro valor.
        json.Should().NotContain("PasswordHash");
        json.Should().NotContain("RefreshToken");
        json.Should().NotContain("SecurityStamp");
        json.Should().NotContain("ConcurrencyStamp");
        json.Should().NotContain("TwoFactorPhoneEncrypted");
        json.Should().NotContain("SsnEncrypted");
        json.Should().NotContain("EinEncrypted");
        json.Should().NotContain("AuthenticatorKey");

        // El JSON usa camelCase en la API; se comprueban las dos formas para que la prueba no
        // dependa de la convención de nombres configurada.
        json.Should().NotContain("passwordHash");
        json.Should().NotContain("refreshToken");
        json.Should().NotContain("securityStamp");
        json.Should().NotContain("concurrencyStamp");
        json.Should().NotContain("twoFactorPhoneEncrypted");
        json.Should().NotContain("ssnEncrypted");

        // Y el número de teléfono completo tampoco, ni suelto ni dentro del enmascarado.
        json.Should().NotContain("+14155552671");
    }

    [Fact]
    public async Task PersonalData_WhenUserNotFound_ReturnsUserNotFound()
    {
        var db     = await DbWithProfileAsync();
        var result = await Run(null, db);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task PersonalData_WhenUserInactive_ReturnsUserNotFound()
    {
        var db   = await DbWithProfileAsync();
        var user = Member();
        user.IsActive = false;

        var result = await Run(user, db);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
