using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Authn.Abstractions;
using MLMConquerorGlobalEdition.Authn.Models;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Commands.Login;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Auth;

public class LoginHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> DateTimeProvider()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IJwtService> CreateJwtService(
        string accessToken = "access-token",
        string refreshToken = "refresh-token",
        TimeSpan? accessExpiry = null,
        TimeSpan? refreshExpiry = null)
    {
        var jwt = new Mock<IJwtService>();
        jwt.Setup(j => j.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(accessToken);
        jwt.Setup(j => j.GenerateRefreshToken()).Returns(refreshToken);
        jwt.Setup(j => j.AccessTokenExpiry).Returns(accessExpiry ?? TimeSpan.FromMinutes(60));
        jwt.Setup(j => j.RefreshTokenExpiry).Returns(refreshExpiry ?? TimeSpan.FromDays(30));
        return jwt;
    }

    /// <summary>
    /// El doble de la librería <c>Authn</c>. El handler ya no genera el código, ni lo hashea,
    /// ni manda el correo: pide un challenge y devuelve lo que le den.
    /// </summary>
    private static Mock<ITwoFactorService> CreateTwoFactorService(
        string           challenge    = "challenge-jwt",
        TwoFactorChannel channel      = TwoFactorChannel.Email,
        string           maskedTarget = "u***@test.com")
    {
        var m = new Mock<ITwoFactorService>();
        m.Setup(s => s.IssueAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
                It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChallengeIssued>.Success(
                new ChallengeIssued(challenge, channel, maskedTarget, FixedNow.AddMinutes(5))));
        m.Setup(s => s.IssueEnrollmentToken(It.IsAny<ApplicationUser>())).Returns("enrollment-jwt");
        return m;
    }

    /// <summary>
    /// Configuración de producción por defecto: <c>MandatoryRoles</c> ausente, que es lo mismo
    /// que vacío. Nadie es forzado a enrolarse mientras no se llene la lista.
    /// </summary>
    private static IConfiguration CreateConfig(params string[] mandatoryRoles)
    {
        var data = new Dictionary<string, string?>();
        for (var i = 0; i < mandatoryRoles.Length; i++)
            data[$"Auth:TwoFactor:MandatoryRoles:{i}"] = mandatoryRoles[i];

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static LoginHandler BuildHandler(
        Mock<UserManager<ApplicationUser>> userManager,
        AppDbContext? db = null,
        Mock<IJwtService>? jwt = null,
        Mock<IDateTimeProvider>? dateTime = null,
        Mock<ITwoFactorService>? twoFactor = null,
        IConfiguration? config = null)
        => new(
            userManager.Object,
            (jwt       ?? CreateJwtService()).Object,
            (dateTime  ?? DateTimeProvider()).Object,
            db        ?? InMemoryDbHelper.Create(),
            (twoFactor ?? CreateTwoFactorService()).Object,
            config    ?? CreateConfig());

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("notfound@test.com"))
                   .ReturnsAsync((ApplicationUser?)null);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "notfound@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenUserIsInactive_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        var inactiveUser = new ApplicationUser { Id = "user-001", Email = "inactive@test.com", IsActive = false };
        userManager.Setup(m => m.FindByEmailAsync("inactive@test.com")).ReturnsAsync(inactiveUser);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "inactive@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenPasswordIsInvalid_ReturnsInvalidCredentials()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser { Id = "user-001", Email = "user@test.com", IsActive = true };
        userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "wrong-password")).ReturnsAsync(false);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "user@test.com", Password = "wrong-password" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_WhenAmbassadorLogin_ReturnsMemberTypeAmbassador()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id = "user-001", Email = "amb@test.com", IsActive = true, MemberProfileId = "AMB-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("amb@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct-pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = CreateJwtService("access-tok", "refresh-tok");
        var handler = BuildHandler(userManager, jwt: jwt);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "amb@test.com", Password = "correct-pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeFalse();
        result.Value.MemberType.Should().Be("Ambassador");
        result.Value.MemberId.Should().Be("AMB-000001");
        result.Value.AccessToken.Should().Be("access-tok");
        result.Value.RefreshToken.Should().Be("refresh-tok");
        result.Value.TokenExpiry.Should().Be(FixedNow.AddMinutes(60));
    }

    [Fact]
    public async Task Handle_WhenMemberLogin_ReturnsMemberTypeMember()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id = "user-002", Email = "member@test.com", IsActive = true, MemberProfileId = "MBR-000001"
        };
        userManager.Setup(m => m.FindByEmailAsync("member@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "member@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberType.Should().Be("Member");
    }

    [Fact]
    public async Task Handle_WhenAdminLogin_ReturnsMemberTypeStaff()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser { Id = "user-003", Email = "admin@test.com", IsActive = true, MemberProfileId = null };
        userManager.Setup(m => m.FindByEmailAsync("admin@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "admin@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberType.Should().Be("Staff");
    }

    [Fact]
    public async Task Handle_WhenLoginSucceeds_StoresHashedRefreshTokenOnUser()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser { Id = "user-001", Email = "amb@test.com", IsActive = true, MemberProfileId = "AMB-000001" };
        userManager.Setup(m => m.FindByEmailAsync("amb@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Ambassador" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var jwt = CreateJwtService(refreshToken: "raw-refresh-token");
        var handler = BuildHandler(userManager, jwt: jwt);

        await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "amb@test.com", Password = "pass" }),
            CancellationToken.None);

        user.RefreshToken.Should().NotBeNullOrEmpty();
        user.RefreshToken.Should().NotBe("raw-refresh-token");
        user.RefreshTokenExpiry.Should().Be(FixedNow.AddDays(30));
        user.LastLoginAt.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Handle_WhenTwoFactorEnabled_ReturnsChallengeAndDoesNotIssueTokens()
    {
        var userManager = UserManagerHelper.Create();
        var user = new ApplicationUser
        {
            Id                = "user-2fa",
            Email             = "tfa@test.com",
            IsActive          = true,
            TwoFactorEnabled  = true,
            MemberProfileId   = null
        };
        userManager.Setup(m => m.FindByEmailAsync("tfa@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        // Los roles se resuelven antes de la rama de dos factores: la de enrolamiento
        // obligatorio decide sobre ellos.
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        var twoFactor = CreateTwoFactorService(challenge: "issued-jwt", maskedTarget: "t***@test.com");
        var handler   = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "tfa@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.ChallengeToken.Should().Be("issued-jwt");
#pragma warning disable CS0618 // MaskedEmail sigue siendo el contrato de los clientes actuales.
        result.Value.MaskedEmail.Should().Be("t***@test.com");
#pragma warning restore CS0618
        result.Value.AccessToken.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeEmpty();

        // No refresh-token persistence yet — that happens after successful verify.
        user.RefreshToken.Should().BeNull();
        user.LastLoginAt.Should().BeNull();
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserLockedOut_ReturnsAccountLocked()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Email = "locked@test.com", UserName = "locked@test.com", IsActive = true
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("locked@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(true);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "locked@test.com", Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ACCOUNT_LOCKED");

        // No debe llegar a comprobar la contraseña de una cuenta bloqueada.
        userManager.Verify(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
                           Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordInvalid_IncrementsAccessFailedCount()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Email = "user@test.com", UserName = "user@test.com", IsActive = true
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(m => m.CheckPasswordAsync(user, "wrong")).ReturnsAsync(false);
        userManager.Setup(m => m.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "user@test.com", Password = "wrong" }),
            CancellationToken.None);

        result.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        userManager.Verify(m => m.AccessFailedAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPasswordValid_ResetsAccessFailedCount()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Email = "user@test.com", UserName = "user@test.com",
            IsActive = true, TwoFactorEnabled = false
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync("user@test.com")).ReturnsAsync(user);
        userManager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct")).ReturnsAsync(true);
        userManager.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var handler = BuildHandler(userManager);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = "user@test.com", Password = "correct" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        userManager.Verify(m => m.ResetAccessFailedCountAsync(user), Times.Once);
    }

    // ── delegación en la librería Authn ──────────────────────────────────────

    private static ApplicationUser TwoFactorUser(
        string           email     = "tfa@test.com",
        TwoFactorChannel preferred = TwoFactorChannel.Email,
        string?          memberId  = null) => new()
    {
        Id                        = "user-2fa",
        Email                     = email,
        UserName                  = email,
        IsActive                  = true,
        TwoFactorEnabled          = true,
        PreferredTwoFactorChannel = preferred,
        MemberProfileId           = memberId
    };

    /// <summary>
    /// 1. El handler delega la emisión del código en <c>ITwoFactorService</c> y no vuelve a
    /// hacerla a mano. La ausencia de <c>IEmailService</c> en el constructor es parte de la
    /// afirmación: sin esa dependencia el handler no puede mandar un correo aunque quiera, y
    /// por eso se comprueba aquí en vez de con un <c>Times.Never</c> sobre un doble que ya no
    /// se le inyecta y que pasaría siempre.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTwoFactorEnabled_DelegatesToTwoFactorService()
    {
        var user        = TwoFactorUser();
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });

        var twoFactor = CreateTwoFactorService();
        var handler   = BuildHandler(userManager, twoFactor: twoFactor);

        await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        twoFactor.Verify(s => s.IssueAsync(
            user, TwoFactorPurpose.Login, null, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        twoFactor.VerifyNoOtherCalls();

        // El envío del código ya no lo hace el handler: no tiene con qué. Si alguien vuelve a
        // meter IEmailService en el constructor, esta prueba lo dice.
        typeof(LoginHandler).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType)
            .Should().NotContain(typeof(IEmailService));
    }

    /// <summary>2. El canal y el destino enmascarado llegan tal cual a la respuesta.</summary>
    [Fact]
    public async Task Handle_WhenTwoFactorEnabled_ReturnsChannelAndMaskedTarget()
    {
        var user        = TwoFactorUser(preferred: TwoFactorChannel.Sms);
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });

        var twoFactor = CreateTwoFactorService(
            channel: TwoFactorChannel.Sms, maskedTarget: "********2671");
        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.Channel.Should().Be(TwoFactorChannel.Sms);
        result.Value.MaskedTarget.Should().Be("********2671");

        // Un teléfono no es un correo: MaskedEmail se queda vacío en vez de enseñar el número
        // en un campo que la interfaz presenta como dirección de correo.
#pragma warning disable CS0618
        result.Value.MaskedEmail.Should().BeNull();
#pragma warning restore CS0618
    }

    /// <summary>
    /// 3. Rol en <c>MandatoryRoles</c> y sin 2FA configurado: enrolamiento obligatorio y ni un
    /// solo token de acceso.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRoleIsMandatoryAndNotEnrolled_ReturnsRequiresEnrollment()
    {
        var user = new ApplicationUser
        {
            Id = "user-adm", Email = "admin@test.com", UserName = "admin@test.com",
            IsActive = true, TwoFactorEnabled = false
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        var twoFactor = CreateTwoFactorService();
        var handler   = BuildHandler(userManager, twoFactor: twoFactor, config: CreateConfig("Admin"));

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresEnrollment.Should().BeTrue();
        result.Value.EnrollmentToken.Should().Be("enrollment-jwt");

        // Nada de acceso hasta enrolarse: sin esto el "enrolamiento obligatorio" sería un
        // cartel que el cliente puede ignorar mientras se guarda los tokens.
        result.Value.AccessToken.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeEmpty();
        result.Value.RequiresTwoFactor.Should().BeFalse();
        user.RefreshToken.Should().BeNull();
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);

        // Tampoco se manda ningún código: no hay dónde recibirlo todavía.
        twoFactor.Verify(s => s.IssueAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
            It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// 4. Con la lista puesta pero un rol que no está en ella, el login sigue igual que
    /// siempre: el enrolamiento forzado se aplica por rol, no a todo el mundo.
    /// </summary>
    [Fact]
    public async Task Handle_WhenRoleIsNotMandatoryAndNotEnrolled_LogsInNormally()
    {
        var user = new ApplicationUser
        {
            Id = "user-mbr", Email = "member@test.com", UserName = "member@test.com",
            IsActive = true, TwoFactorEnabled = false, MemberProfileId = "MBR-000001"
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var twoFactor = CreateTwoFactorService();
        var handler   = BuildHandler(userManager, twoFactor: twoFactor, config: CreateConfig("Admin"));

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresEnrollment.Should().BeFalse();
        result.Value.EnrollmentToken.Should().BeNull();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.MemberType.Should().Be("Member");
        twoFactor.Verify(s => s.IssueEnrollmentToken(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// 5. El default de producción: <c>MandatoryRoles</c> vacío. Nadie queda atrapado en el
    /// enrolamiento mientras no se llene la lista a mano.
    /// </summary>
    [Fact]
    public async Task Handle_WhenMandatoryRolesEmpty_NeverRequiresEnrollment()
    {
        var user = new ApplicationUser
        {
            Id = "user-adm", Email = "admin@test.com", UserName = "admin@test.com",
            IsActive = true, TwoFactorEnabled = false
        };

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var twoFactor = CreateTwoFactorService();
        var handler   = BuildHandler(userManager, twoFactor: twoFactor, config: CreateConfig());

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresEnrollment.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.MemberType.Should().Be("Staff");
        twoFactor.Verify(s => s.IssueEnrollmentToken(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// 6. Si el transporte no entrega, el login devuelve ese mismo error en vez de tokens: un
    /// código que no llegó no puede convertirse en una sesión abierta.
    /// </summary>
    [Fact]
    public async Task Handle_WhenIssueFails_ReturnsThatError()
    {
        var user        = TwoFactorUser();
        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });

        var twoFactor = new Mock<ITwoFactorService>();
        twoFactor.Setup(s => s.IssueAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<TwoFactorPurpose>(), It.IsAny<string?>(),
                It.IsAny<TwoFactorChannel?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ChallengeIssued>.Failure(
                "CHANNEL_UNAVAILABLE", "No se pudo entregar el código por Email."));

        var handler = BuildHandler(userManager, twoFactor: twoFactor);

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CHANNEL_UNAVAILABLE");
        result.Error.Should().Be("No se pudo entregar el código por Email.");
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    /// <summary>
    /// 7. Regresión del BizCenter. <c>SignupAPI</c> sirve al portal de administración y al
    /// BizCenter a la vez, y <c>PreferredTwoFactorChannel</c> vale <c>Email</c> para todos los
    /// usuarios existentes. Un miembro con 2FA activo tiene que seguir recibiendo exactamente
    /// la misma respuesta que antes de pasar por la librería.
    /// </summary>
    [Fact]
    public async Task Handle_MemberWithEmailTwoFactor_BehavesAsBefore()
    {
        var user = TwoFactorUser(
            email: "miembro@test.com", preferred: TwoFactorChannel.Email, memberId: "MBR-000001");

        var userManager = UserManagerHelper.Create();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "pass")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });

        var twoFactor = CreateTwoFactorService(
            challenge: "issued-jwt", channel: TwoFactorChannel.Email, maskedTarget: "m*******@test.com");

        // MandatoryRoles vacío, como en producción: el miembro no debe acabar en enrolamiento.
        var handler = BuildHandler(userManager, twoFactor: twoFactor, config: CreateConfig());

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest { Email = user.Email!, Password = "pass" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RequiresTwoFactor.Should().BeTrue();
        result.Value.ChallengeToken.Should().Be("issued-jwt");
#pragma warning disable CS0618 // El contrato viejo se conserva: los clientes actuales leen MaskedEmail.
        result.Value.MaskedEmail.Should().Be("m*******@test.com");
#pragma warning restore CS0618
        result.Value.MaskedTarget.Should().Be("m*******@test.com");
        result.Value.Channel.Should().Be(TwoFactorChannel.Email);

        // Ni enrolamiento forzado ni tokens: exactamente el mismo trato que antes.
        result.Value.RequiresEnrollment.Should().BeFalse();
        result.Value.EnrollmentToken.Should().BeNull();
        result.Value.AccessToken.Should().BeEmpty();
        result.Value.RefreshToken.Should().BeEmpty();
        userManager.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);

        // Y el código sale por correo, que es el canal preferido de todos los usuarios de hoy.
        twoFactor.Verify(s => s.IssueAsync(
            user, TwoFactorPurpose.Login, null, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
