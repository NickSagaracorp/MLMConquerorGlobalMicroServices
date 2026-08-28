using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Phone;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

/// <summary>
/// Redención del código que dio de alta el teléfono. Solo aquí pasa
/// <c>TwoFactorPhoneConfirmed</c> a true: hasta que alguien demuestra tener el número, el canal
/// SMS no existe para esa cuenta.
/// </summary>
public class VerifyPhoneHandlerTests
{
    private const string UserId = "user-001";
    private const string Email  = "usuario@dominio.com";
    private const string Token  = "challenge-token";
    private const string Code   = "123456";

    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITwoFactorService> _twoFactor = new();

    private static ChallengeClaims Claims(string userId = UserId) => new(
        Jti:          "jti-phone",
        UserId:       userId,
        Email:        Email,
        Purpose:      TwoFactorPurpose.Enrollment,
        OperationKey: null,
        Channel:      TwoFactorChannel.Sms,
        CodeHash:     "hash",
        IssuedAt:     FixedNow,
        ExpiresAt:    FixedNow.AddMinutes(5));

    public VerifyPhoneHandlerTests()
    {
        // Solo el código bueno verifica; cualquier otro devuelve CODE_INVALID, igual que la
        // librería de verdad.
        _twoFactor.Setup(t => t.VerifyAsync(
                      It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(),
                      It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Failure(
                      "CODE_INVALID", "El código introducido no es válido."));

        _twoFactor.Setup(t => t.VerifyAsync(
                      Token, Code, TwoFactorPurpose.Enrollment,
                      It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims()));
    }

    private static Mock<UserManager<ApplicationUser>> UserManagerWithUser(out ApplicationUser user)
    {
        user = new ApplicationUser
        {
            Id                        = UserId,
            Email                     = Email,
            IsActive                  = true,
            PreferredTwoFactorChannel = TwoFactorChannel.Email,
            TwoFactorPhoneEncrypted   = "ENC:+14155552671",
            TwoFactorPhoneLast4       = "2671",
            TwoFactorPhoneConfirmed   = false
        };
        var captured = user;

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync(UserId)).ReturnsAsync(captured);
        userManager.Setup(m => m.UpdateAsync(captured)).ReturnsAsync(IdentityResult.Success);
        return userManager;
    }

    private VerifyPhoneHandler BuildHandler(Mock<UserManager<ApplicationUser>> userManager)
        => new(userManager.Object, _twoFactor.Object);

    private static VerifyPhoneCommand Command(string code = Code) =>
        new(UserId, new VerifyPhoneRequest { ChallengeToken = Token, Code = code });

    [Fact]
    public async Task VerifyPhone_WhenCodeCorrect_ConfirmsPhone()
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.TwoFactorPhoneConfirmed.Should().BeTrue();

        // El número no se toca: lo que se confirma es el que ya estaba guardado.
        user.TwoFactorPhoneEncrypted.Should().Be("ENC:+14155552671");
        user.TwoFactorPhoneLast4.Should().Be("2671");

        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task VerifyPhone_WhenCodeWrong_DoesNotConfirm()
    {
        var userManager = UserManagerWithUser(out var user);
        var handler     = BuildHandler(userManager);

        var result = await handler.Handle(Command("999999"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");

        user.TwoFactorPhoneConfirmed.Should().BeFalse();
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// El challenge tiene que ser del mismo usuario que trae el token de acceso. Sin esta
    /// comprobación, quien consiguiera un challenge ajeno confirmaría el teléfono de su propia
    /// cuenta —o peor, el de la otra— con un código que no es suyo.
    /// </summary>
    [Fact]
    public async Task VerifyPhone_WhenChallengeBelongsToAnotherUser_DoesNotConfirm()
    {
        var userManager = UserManagerWithUser(out var user);

        _twoFactor.Setup(t => t.VerifyAsync(
                      Token, Code, TwoFactorPurpose.Enrollment,
                      It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims(userId: "otro-usuario")));

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");

        user.TwoFactorPhoneConfirmed.Should().BeFalse();
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// El challenge tiene que venir del canal SMS. El token de enrolamiento TOTP comparte
    /// propósito (<see cref="TwoFactorPurpose.Enrollment"/>) y sin este corte serviría para
    /// marcar un teléfono como confirmado con un código de la aplicación de autenticación, sin
    /// que nadie haya recibido nunca el SMS.
    /// </summary>
    [Fact]
    public async Task VerifyPhone_WhenChallengeIsNotSms_DoesNotConfirm()
    {
        var userManager = UserManagerWithUser(out var user);

        _twoFactor.Setup(t => t.VerifyAsync(
                      Token, Code, TwoFactorPurpose.Enrollment,
                      It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Result<ChallengeClaims>.Success(Claims() with
                  {
                      Channel = TwoFactorChannel.Authenticator
                  }));

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
        user.TwoFactorPhoneConfirmed.Should().BeFalse();
    }
}
