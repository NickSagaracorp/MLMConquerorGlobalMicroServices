using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.ResendTwoFactor;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class ResendTwoFactorHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ChallengeClaims Claims(TwoFactorChannel channel = TwoFactorChannel.Email) => new(
        Jti:          "jti-old",
        UserId:       "user-2fa",
        Email:        "tfa@test.com",
        Purpose:      TwoFactorPurpose.Login,
        OperationKey: null,
        Channel:      channel,
        CodeHash:     channel == TwoFactorChannel.Authenticator ? null : "old-hash",
        IssuedAt:     FixedNow.AddMinutes(-5),
        ExpiresAt:    FixedNow);

    /// <summary>
    /// El challenge se valida contra <c>IChallengeTokenService</c> y no contra
    /// <c>VerifyAsync</c>: el reenvío admite tokens vencidos dentro de la ventana de gracia, y
    /// <c>VerifyAsync</c> no tiene por dónde permitirlo — ni debe.
    /// </summary>
    private static Mock<IChallengeTokenService> CreateChallengeService(
        Result<ChallengeClaims>? validationResult = null)
    {
        var m = new Mock<IChallengeTokenService>();
        m.Setup(s => s.Validate(It.IsAny<string>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(), true))
         .Returns(validationResult ?? Result<ChallengeClaims>.Success(Claims()));
        return m;
    }

    private static Mock<ITwoFactorService> CreateTwoFactorService(
        string           challenge    = "fresh-jwt",
        TwoFactorChannel channel      = TwoFactorChannel.Email,
        string           maskedTarget = "t***@test.com")
    {
        var m = new Mock<ITwoFactorService>();
        m.Setup(s => s.IssueAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
                It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChallengeIssued>.Success(
                new ChallengeIssued(challenge, channel, maskedTarget, FixedNow.AddMinutes(5))));
        return m;
    }

    private static ResendTwoFactorHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<IChallengeTokenService>? challenges = null,
        Mock<ITwoFactorService>? twoFactor = null)
        => new(
            userManager.Object,
            db          ?? InMemoryDbHelper.Create(),
            (challenges ?? CreateChallengeService()).Object,
            (twoFactor  ?? CreateTwoFactorService()).Object);

    private static Mock<UserManager<ApplicationUser>> UserManagerWithTfaUser()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-2fa"))
                   .ReturnsAsync(new ApplicationUser
                   {
                       Id               = "user-2fa",
                       Email            = "tfa@test.com",
                       IsActive         = true,
                       TwoFactorEnabled = true
                   });
        return userManager;
    }

    [Fact]
    public async Task Handle_WhenChallengeBeyondGraceWindow_ReturnsCodeExpired()
    {
        var userManager = UserManagerHelper.Create();
        var challenges = CreateChallengeService(
            Result<ChallengeClaims>.Failure("CODE_EXPIRED", "Challenge is too old to resend; please log in again."));
        var handler = BuildHandler(userManager, challenges: challenges);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "old-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_EXPIRED");
    }

    [Fact]
    public async Task Handle_WhenSignatureInvalid_ReturnsInvalidChallenge()
    {
        var userManager = UserManagerHelper.Create();
        var challenges = CreateChallengeService(
            Result<ChallengeClaims>.Failure("INVALID_CHALLENGE", "Challenge token is invalid."));
        var handler = BuildHandler(userManager, challenges: challenges);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "tampered" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    [Fact]
    public async Task Handle_WhenUserNoLongerHasTfaEnabled_ReturnsInvalidChallenge()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByIdAsync("user-2fa"))
                   .ReturnsAsync(new ApplicationUser
                   {
                       Id               = "user-2fa",
                       Email            = "tfa@test.com",
                       IsActive         = true,
                       TwoFactorEnabled = false
                   });

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CHALLENGE");
    }

    /// <summary>
    /// El reenvío tiene que aceptar un challenge ya vencido: es justo el caso en el que el
    /// usuario pulsa "reenviar". Validarlo sin <c>allowExpired</c> lo obligaría a volver a
    /// escribir su contraseña, que es lo que la ventana de gracia existe para evitar.
    /// </summary>
    [Fact]
    public async Task Handle_ValidatesChallengeAllowingExpired()
    {
        var userManager = UserManagerWithTfaUser();
        var challenges  = CreateChallengeService();
        var handler     = BuildHandler(userManager, challenges: challenges);

        await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        challenges.Verify(s => s.Validate("valid-jwt", TwoFactorPurpose.Login, null, true), Times.Once);
    }

    /// <summary>
    /// Con Authenticator no hay nada que reenviar: el código lo genera la aplicación del usuario
    /// en su teléfono. Sin este corte, el botón de reenviar de la interfaz emitiría un challenge
    /// nuevo y gastaría cupo de emisiones sin mandar absolutamente nada.
    /// </summary>
    [Fact]
    public async Task Handle_WhenChannelIsAuthenticator_ReturnsChannelUnavailableAndIssuesNothing()
    {
        var userManager = UserManagerWithTfaUser();
        var challenges  = CreateChallengeService(
            Result<ChallengeClaims>.Success(Claims(TwoFactorChannel.Authenticator)));
        var twoFactor   = CreateTwoFactorService();

        var handler = BuildHandler(userManager, challenges: challenges, twoFactor: twoFactor);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        result.Error.Should().Contain("aplicación de autenticación");

        twoFactor.Verify(s => s.IssueAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
            It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenChallengeIsFresh_IssuesNewChallengeThroughTheLibrary()
    {
        var userManager = UserManagerWithTfaUser();
        var twoFactor   = CreateTwoFactorService();
        var handler     = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.ChallengeToken.Should().Be("fresh-jwt");
        result.Value.Channel.Should().Be(TwoFactorChannel.Email);
        result.Value.MaskedTarget.Should().Be("t***@test.com");
#pragma warning disable CS0618 // MaskedEmail sigue siendo el contrato de los clientes actuales.
        result.Value.MaskedEmail.Should().Be("t***@test.com");
#pragma warning restore CS0618
        result.Value.AccessToken.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeEmpty();

        // Se reenvía por el mismo canal que emitió el challenge original, y con el propósito
        // Login: el código nuevo tiene que servir en la misma pantalla de verificación.
        twoFactor.Verify(s => s.IssueAsync(
            It.IsAny<ApplicationUser>(), TwoFactorPurpose.Login, null,
            TwoFactorChannel.Email, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Si el transporte no entrega —o el usuario agotó su cupo de emisiones— el error se
    /// propaga tal cual en vez de devolver un challenge para un código que nunca salió.
    /// </summary>
    [Fact]
    public async Task Handle_WhenIssueFails_PropagatesThatError()
    {
        var userManager = UserManagerWithTfaUser();

        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor.Setup(s => s.IssueAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
                It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChallengeIssued>.Failure(
                "TOO_MANY_REQUESTS",
                "Se han pedido demasiados códigos; espere unos minutos antes de volver a intentarlo."));

        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new ResendTwoFactorCommand(new ResendTwoFactorRequest { ChallengeToken = "valid-jwt" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TOO_MANY_REQUESTS");
    }
}
