using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Alta del teléfono para el canal SMS del 2FA. El número llega del cuerpo pero el usuario sale
/// siempre del token de acceso: el comando solo conoce un UserId que el controlador saca de las
/// claims, nunca del JSON.
/// </summary>
public class AddPhoneHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";
    private const string Phone  = "+14155552671";

    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<ITwoFactorService>  _twoFactor  = new();

    public AddPhoneHandlerTests()
    {
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(p => "ENC:" + p);

        _twoFactor.Setup(t => t.IssueAsync(
                      It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
                      It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeIssued>.Success(new ChallengeIssued(
                      ChallengeToken: "challenge-token",
                      Channel:        TwoFactorChannel.Sms,
                      MaskedTarget:   "********2671",
                      ExpiresAt:      FixedNow.AddMinutes(5))));
    }

    private Mock<UserManager<ApplicationUser>> UserManagerWithUser(
        out ApplicationUser user,
        bool               phoneConfirmed = false,
        string?            encryptedPhone = null,
        TwoFactorChannel   preferred      = TwoFactorChannel.Email)
    {
        user = new ApplicationUser
        {
            Id                        = UserId,
            Email                     = Email,
            IsActive                  = true,
            PreferredTwoFactorChannel = preferred,
            TwoFactorPhoneEncrypted   = encryptedPhone,
            TwoFactorPhoneLast4       = encryptedPhone is null ? null : "0000",
            TwoFactorPhoneConfirmed   = phoneConfirmed
        };
        var captured = user;

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(captured);
        userManager.Setup(m => m.UpdateAsync(captured)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private AddPhoneHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager, AppDbContext? db = null)
        => new(userManager.Object, _encryption.Object, _twoFactor.Object, db ?? InMemoryDbHelper.Create());

    private static AddPhoneCommand Command(string phone = Phone) =>
        new(UserId, new AddPhoneRequest { PhoneE164 = phone });

    private void VerifyNoCodeIssued() =>
        _twoFactor.Verify(t => t.IssueAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
            It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

    /// <summary>
    /// El número se guarda cifrado —es a la vez PII y factor de autenticación— y con los cuatro
    /// últimos dígitos aparte, en claro y a propósito: son los que permiten enmascararlo en
    /// pantalla sin descifrar nada en cada carga.
    /// </summary>
    [Fact]
    public async Task AddPhone_EncryptsNumberAndStoresLast4()
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        _encryption.Verify(e => e.Encrypt(Phone), Times.Once);

        // Lo que se guarda es lo que devolvió el cifrador, no el número tal cual.
        user.TwoFactorPhoneEncrypted.Should().Be("ENC:" + Phone);
        user.TwoFactorPhoneEncrypted.Should().NotBe(Phone);
        user.TwoFactorPhoneLast4.Should().Be("2671");

        // Todavía no está verificado: lo único que se ha hecho es mandarle un código.
        user.TwoFactorPhoneConfirmed.Should().BeFalse();

        userManager.Verify(m => m.UpdateAsync(user), Times.AtLeastOnce);
    }

    /// <summary>
    /// Formato inválido: se corta antes de emitir. Sin este corte, un número mal formado
    /// gastaría cupo de emisiones y reventaría dentro de Twilio como una excepción, no como un
    /// error de dominio.
    /// </summary>
    [Theory]
    [InlineData("14155552671")]      // sin '+'
    [InlineData("+1 415 555 2671")]  // con separadores
    [InlineData("+1415552")]         // menos de 8 dígitos
    [InlineData("+1234567890123456")]// más de 15 dígitos
    [InlineData("+1415555267a")]     // no numérico
    [InlineData("")]
    public async Task AddPhone_WhenNotE164_ReturnsInvalidPhone(string phone)
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(Command(phone), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_PHONE");

        VerifyNoCodeIssued();
        _encryption.Verify(e => e.Encrypt(It.IsAny<string>()), Times.Never);
        user.TwoFactorPhoneEncrypted.Should().BeNull();
    }

    /// <summary>
    /// El SMS sale por <see cref="ITwoFactorService"/>, nunca por <c>ISmsService</c> directo.
    /// Este endpoint acepta un número arbitrario del usuario: saltarse la librería se saltaría
    /// también el tope de tres emisiones cada quince minutos, y el endpoint se convertiría en
    /// una herramienta para mandar SMS a cualquier teléfono a costa de la empresa.
    /// </summary>
    [Fact]
    public async Task AddPhone_SendsCodeThroughTwoFactorService()
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.ChallengeToken.Should().Be("challenge-token");

        // Canal forzado a SMS: da igual cuál sea el preferido del usuario, lo que hay que
        // verificar es el teléfono. Propósito Enrollment: es el único con el que la librería
        // acepta un SMS todavía sin confirmar.
        _twoFactor.Verify(t => t.IssueAsync(
            user,
            TwoFactorPurpose.Enrollment,
            It.IsAny<string?>(),
            TwoFactorChannel.Sms,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// El tope de emisiones y las caídas del transporte se propagan tal cual: quien recibe
    /// TOO_MANY_REQUESTS tiene que enterarse, no ver un éxito por un código que nunca salió.
    /// </summary>
    [Fact]
    public async Task AddPhone_WhenIssueFails_PropagatesError()
    {
        var userManager = UserManagerWithUser(out _);
        var handler     = BuildHandler(userManager);

        _twoFactor.Setup(t => t.IssueAsync(
                      It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
                      It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeIssued>.Failure(
                      "TOO_MANY_REQUESTS", "Se han pedido demasiados códigos."));

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOO_MANY_REQUESTS");
        result.Value.Should().BeNull();
    }

    /// <summary>
    /// Cambiar de número no puede dejar el nuevo confirmado por herencia del anterior: sería un
    /// canal de 2FA verificado sin que nadie haya demostrado tener ese teléfono.
    /// </summary>
    [Fact]
    public async Task AddPhone_WhenReplacingConfirmedPhone_LeavesItUnconfirmed()
    {
        var userManager = UserManagerWithUser(
            out var user,
            phoneConfirmed: true,
            encryptedPhone: "ENC:+19995550000",
            preferred:      TwoFactorChannel.Sms);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);

        user.TwoFactorPhoneEncrypted.Should().Be("ENC:" + Phone);
        user.TwoFactorPhoneLast4.Should().Be("2671");
        user.TwoFactorPhoneConfirmed.Should().BeFalse();

        // El canal preferido era SMS y acaba de quedarse sin destino verificado. Dejarlo en SMS
        // significaría que el siguiente inicio de sesión pide un código por un canal que
        // ResolveTarget ya no resuelve: la cuenta se quedaría fuera hasta que alguien la toque
        // a mano. Vuelve a correo hasta que el número nuevo esté confirmado.
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Email);
    }

    [Fact]
    public async Task AddPhone_WhenUserNotFound_ReturnsUserNotFound()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
        VerifyNoCodeIssued();
    }
}
