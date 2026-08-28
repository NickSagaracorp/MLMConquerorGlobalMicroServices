using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.AccountStatus;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Estado de la cuenta para el panel de gestión. El usuario sale siempre del token: el handler
/// solo conoce un UserId que el controlador saca de las claims, nunca de la query.
/// </summary>
public class GetAccountStatusHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";

    private static readonly DateTime Enrolled = new(2026, 4, 1, 9, 30, 0, DateTimeKind.Utc);

    private static ApplicationUser User(
        bool             emailConfirmed  = true,
        string?          encryptedPhone  = null,
        string?          last4           = null,
        bool             phoneConfirmed  = false,
        DateTime?        enrolledAt      = null,
        bool             twoFactorOn     = false,
        TwoFactorChannel preferred       = TwoFactorChannel.Email) => new()
        {
            Id                        = UserId,
            Email                     = Email,
            EmailConfirmed            = emailConfirmed,
            IsActive                  = true,
            TwoFactorEnabled          = twoFactorOn,
            PreferredTwoFactorChannel = preferred,
            TwoFactorEnrolledAt       = enrolledAt,
            TwoFactorPhoneEncrypted   = encryptedPhone,
            TwoFactorPhoneLast4       = last4,
            TwoFactorPhoneConfirmed   = phoneConfirmed
        };

    private static GetAccountStatusHandler Handler(ApplicationUser? user, bool hasPassword = true)
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(user);
        userManager.Setup(m => m.HasPasswordAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(hasPassword);
        return new GetAccountStatusHandler(userManager.Object);
    }

    private static Task<MLMConquerorGlobalEdition.SharedKernel.Result<
        MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.AccountStatusResponse>> Run(
        ApplicationUser? user, bool hasPassword = true)
        => Handler(user, hasPassword).Handle(new GetAccountStatusQuery(UserId), CancellationToken.None);

    // ── enmascarado ──────────────────────────────────────────────────────────

    /// <summary>
    /// El teléfono sale solo enmascarado y construido desde <c>TwoFactorPhoneLast4</c>, que se
    /// guarda en claro justo para esto. Descifrar el número entero para mandarlo a la interfaz lo
    /// dejaría en el cuerpo de la respuesta y en cualquier registro intermedio a cambio de nada:
    /// la pantalla solo enseña cuatro dígitos.
    /// </summary>
    [Fact]
    public async Task AccountStatus_MasksPhoneAndNeverReturnsItInClear()
    {
        var user = User(encryptedPhone: "ENC:+14155552671", last4: "2671", phoneConfirmed: true);

        var result = await Run(user);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.MaskedPhone.Should().Be("********2671");
        result.Value.MaskedPhone.Should().EndWith("2671");

        // Ni el cifrado ni nada que se le parezca al número completo.
        result.Value.MaskedPhone.Should().NotContain("ENC:");
        result.Value.MaskedPhone.Should().NotContain("+1415");

        result.Value.HasPhone.Should().BeTrue();
        result.Value.PhoneConfirmed.Should().BeTrue();
    }

    /// <summary>Sin teléfono dado de alta no hay nada que enmascarar.</summary>
    [Fact]
    public async Task AccountStatus_WhenNoPhone_MaskedPhoneIsNull()
    {
        var result = await Run(User());

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.MaskedPhone.Should().BeNull();
        result.Value.HasPhone.Should().BeFalse();
        result.Value.PhoneConfirmed.Should().BeFalse();
    }

    // ── canales disponibles ──────────────────────────────────────────────────

    /// <summary>
    /// Cuenta recién creada: solo correo. La pantalla no debe ofrecer SMS ni autenticador, porque
    /// elegirlos dejaría al usuario pidiendo un código que la librería no sabe entregar y le
    /// cerraría la puerta en su siguiente inicio de sesión.
    /// </summary>
    [Fact]
    public async Task AvailableChannels_WithNothingConfigured_OnlyEmail()
    {
        var result = await Run(User());

        result.Value!.AvailableChannels.Should().Equal(TwoFactorChannel.Email);
    }

    /// <summary>SMS solo con el teléfono <b>confirmado</b>: un número sin verificar no es un factor.</summary>
    [Fact]
    public async Task AvailableChannels_WithUnconfirmedPhone_DoesNotOfferSms()
    {
        var user = User(encryptedPhone: "ENC:+14155552671", last4: "2671", phoneConfirmed: false);

        var result = await Run(user);

        result.Value!.AvailableChannels.Should().NotContain(TwoFactorChannel.Sms);
        result.Value.AvailableChannels.Should().Equal(TwoFactorChannel.Email);
    }

    [Fact]
    public async Task AvailableChannels_WithConfirmedPhone_OffersSms()
    {
        var user = User(encryptedPhone: "ENC:+14155552671", last4: "2671", phoneConfirmed: true);

        var result = await Run(user);

        result.Value!.AvailableChannels.Should().BeEquivalentTo(
            new[] { TwoFactorChannel.Email, TwoFactorChannel.Sms });
    }

    /// <summary>
    /// Authenticator solo con el enrolamiento confirmado: sin clave dada de alta no hay nada que
    /// Identity pueda verificar y la pantalla del código no aceptaría ninguno.
    /// </summary>
    [Fact]
    public async Task AvailableChannels_WithoutEnrollment_DoesNotOfferAuthenticator()
    {
        var result = await Run(User(enrolledAt: null));

        result.Value!.AvailableChannels.Should().NotContain(TwoFactorChannel.Authenticator);
    }

    [Fact]
    public async Task AvailableChannels_WithEnrollment_OffersAuthenticator()
    {
        var result = await Run(User(enrolledAt: Enrolled));

        result.Value!.AvailableChannels.Should().BeEquivalentTo(
            new[] { TwoFactorChannel.Email, TwoFactorChannel.Authenticator });
        result.Value.TwoFactorEnrolledAt.Should().Be(Enrolled);
    }

    [Fact]
    public async Task AvailableChannels_WithEverythingConfigured_OffersTheThree()
    {
        var user = User(
            encryptedPhone: "ENC:+14155552671",
            last4:          "2671",
            phoneConfirmed: true,
            enrolledAt:     Enrolled,
            twoFactorOn:    true,
            preferred:      TwoFactorChannel.Sms);

        var result = await Run(user);

        result.Value!.AvailableChannels.Should().BeEquivalentTo(
            new[] { TwoFactorChannel.Email, TwoFactorChannel.Sms, TwoFactorChannel.Authenticator });

        result.Value.TwoFactorEnabled.Should().BeTrue();
        result.Value.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Sms);
    }

    // ── contraseña ───────────────────────────────────────────────────────────

    /// <summary>
    /// HasPassword refleja lo que dice Identity, no una suposición. El panel decide con esto si
    /// enseña "cambiar contraseña" o "fijar contraseña".
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountStatus_HasPassword_ReflectsIdentity(bool hasPassword)
    {
        var result = await Run(User(), hasPassword);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.HasPassword.Should().Be(hasPassword);
    }

    // ── correo y cuenta ──────────────────────────────────────────────────────

    [Fact]
    public async Task AccountStatus_ReturnsEmailAndConfirmationFlag()
    {
        var result = await Run(User(emailConfirmed: false));

        result.Value!.Email.Should().Be(Email);
        result.Value.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task AccountStatus_WhenUserNotFound_ReturnsUserNotFound()
    {
        var result = await Run(null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task AccountStatus_WhenUserInactive_ReturnsUserNotFound()
    {
        var user = User();
        user.IsActive = false;

        var result = await Run(user);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }
}
