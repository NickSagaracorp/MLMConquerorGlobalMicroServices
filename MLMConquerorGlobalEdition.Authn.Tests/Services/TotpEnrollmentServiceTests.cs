using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Services;
using MLMConquerorGlobalEdition.Authn.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Authn.Tests.Services;

public class TotpEnrollmentServiceTests
{
    private const string Issuer = "MLMConqueror";

    private static readonly DateTime FixedNow = new(2026, 01, 15, 10, 00, 00, DateTimeKind.Utc);

    private static ApplicationUser BuildUser() => new()
    {
        Id    = "user-001",
        Email = "test@test.com"
    };

    private static IConfiguration BuildConfig(string? issuer = Issuer)
    {
        var data = new Dictionary<string, string?>();
        if (issuer is not null)
            data["Auth:TwoFactor:Issuer"] = issuer;

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    /// <summary>
    /// Fija el reloj de prueba. A diferencia de <c>ChallengeTokenServiceTests.MoveClockTo</c>,
    /// aquí Now y UtcNow pueden fijarse a valores distintos: esta librería persiste
    /// TwoFactorEnrolledAt con Now, y una prueba necesita distinguir cuál se usó de verdad.
    /// </summary>
    private static void MoveClockTo(Mock<IDateTimeProvider> clock, DateTime now, DateTime? utcNow = null)
    {
        clock.Setup(c => c.Now).Returns(now);
        clock.Setup(c => c.UtcNow).Returns(utcNow ?? now);
    }

    private static Mock<IDateTimeProvider> ClockAt(DateTime now, DateTime? utcNow = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        MoveClockTo(clock, now, utcNow);
        return clock;
    }

    private static TotpEnrollmentService BuildService(
        Mock<UserManager<ApplicationUser>> userManager,
        out Mock<IDateTimeProvider>         clock,
        DateTime?                           now = null,
        string?                             issuer = Issuer)
    {
        clock = ClockAt(now ?? FixedNow);
        return new TotpEnrollmentService(userManager.Object, clock.Object, BuildConfig(issuer));
    }

    // ── 1 y 2. BeginAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task BeginAsync_ResetsKeyAndReturnsUri()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();
        const string key = "JBSWY3DPEHPK3PXP";

        userManager.Setup(m => m.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetAuthenticatorKeyAsync(user)).ReturnsAsync(key);

        var service = BuildService(userManager, out _);

        var result = await service.BeginAsync(user);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.SharedKey.Should().Be(key);
        result.Value!.AuthenticatorUri.Should().StartWith("otpauth://totp/");
        result.Value!.AuthenticatorUri.Should().Contain(key);
        result.Value!.AuthenticatorUri.Should().Contain(Uri.EscapeDataString(Issuer));

        userManager.Verify(m => m.ResetAuthenticatorKeyAsync(user), Times.Once);
    }

    [Fact]
    public async Task BeginAsync_ReturnsQrAsPngDataUri()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();

        userManager.Setup(m => m.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetAuthenticatorKeyAsync(user)).ReturnsAsync("JBSWY3DPEHPK3PXP");

        var service = BuildService(userManager, out _);

        var result = await service.BeginAsync(user);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        result.Value!.QrCodePngDataUri.Should().StartWith("data:image/png;base64,");

        var base64 = result.Value!.QrCodePngDataUri["data:image/png;base64,".Length..];
        var bytes  = Convert.FromBase64String(base64);

        // Firma PNG: 89 50 4E 47.
        bytes.Take(4).Should().BeEquivalentTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
    }

    [Fact]
    public async Task BeginAsync_WhenKeyGenerationFails_ReturnsFailure()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();

        userManager.Setup(m => m.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetAuthenticatorKeyAsync(user)).ReturnsAsync((string?)null);

        var service = BuildService(userManager, out _);

        var result = await service.BeginAsync(user);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ENROLLMENT_FAILED");
    }

    // ── 4, 5 y 6. ConfirmAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAsync_WhenCodeValid_EnablesTwoFactor()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();
        const string code = "123456";

        userManager
            .Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            .ReturnsAsync(true);
        userManager.Setup(m => m.SetTwoFactorEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var service = BuildService(userManager, out _, now: FixedNow);

        var result = await service.ConfirmAsync(user, code);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.TwoFactorEnrolledAt.Should().Be(FixedNow);
        user.PreferredTwoFactorChannel.Should().Be(TwoFactorChannel.Authenticator);

        userManager.Verify(m => m.SetTwoFactorEnabledAsync(user, true), Times.Once);
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_WhenCodeInvalid_DoesNotEnable()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();
        const string code = "000000";

        userManager
            .Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            .ReturnsAsync(false);

        var service = BuildService(userManager, out _);

        var result = await service.ConfirmAsync(user, code);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CODE_INVALID");

        userManager.Verify(m => m.SetTwoFactorEnabledAsync(user, It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_UsesServerTimeNotUtc()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();
        const string code = "123456";

        var serverNow = new DateTime(2026, 01, 15, 10, 00, 00, DateTimeKind.Unspecified);
        var utcNow    = new DateTime(2026, 01, 15, 15, 00, 00, DateTimeKind.Utc); // otra zona horaria

        userManager
            .Setup(m => m.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            .ReturnsAsync(true);
        userManager.Setup(m => m.SetTwoFactorEnabledAsync(user, true)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var clock = new Mock<IDateTimeProvider>();
        MoveClockTo(clock, serverNow, utcNow);

        var service = new TotpEnrollmentService(userManager.Object, clock.Object, BuildConfig());

        await service.ConfirmAsync(user, code);

        user.TwoFactorEnrolledAt.Should().Be(serverNow);
        user.TwoFactorEnrolledAt.Should().NotBe(utcNow);
    }

    // ── 7. ResetAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetAsync_DisablesAndClearsEnrollment()
    {
        var userManager = UserManagerHelper.Create();
        var user        = BuildUser();
        user.TwoFactorEnrolledAt      = FixedNow.AddDays(-10);
        user.PreferredTwoFactorChannel = TwoFactorChannel.Authenticator;

        userManager.Setup(m => m.SetTwoFactorEnabledAsync(user, false)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.ResetAuthenticatorKeyAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var service = BuildService(userManager, out _);

        var result = await service.ResetAsync(user);

        result.IsSuccess.Should().BeTrue(because: result.Error);
        user.TwoFactorEnrolledAt.Should().BeNull();

        userManager.Verify(m => m.SetTwoFactorEnabledAsync(user, false), Times.Once);
        userManager.Verify(m => m.ResetAuthenticatorKeyAsync(user), Times.Once);
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }
}
