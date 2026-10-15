using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services.Security;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Services;

/// <summary>
/// El aviso de cambio de factor: a quién va, en qué idioma, y que un fallo del transporte no
/// convierte en error un cambio que ya se guardó.
/// </summary>
public class SecurityFactorNotifierTests : IDisposable
{
    private static readonly DateTime Ahora = new(2026, 10, 14, 10, 30, 0);

    private readonly AppDbContext        _db    = InMemoryDbHelper.Create();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public SecurityFactorNotifierTests() => _clock.Setup(c => c.Now).Returns(Ahora);

    public void Dispose() => _db.Dispose();

    private SecurityFactorNotifier Notifier() =>
        new(_email.Object, _db, _clock.Object, NullLogger<SecurityFactorNotifier>.Instance);

    private static ApplicationUser Usuario(string? memberId = null) => new()
    {
        Id = "user-001", Email = "actual@dominio.com", MemberProfileId = memberId
    };

    private void Enviado(string to, string language, Func<Dictionary<string, string>, bool> variables) =>
        _email.Verify(e => e.SendAsync(
            to, It.IsAny<string>(), language, NotificationEvents.SecurityFactorChanged,
            It.Is<Dictionary<string, string>>(v => variables(v)), It.IsAny<CancellationToken>()), Times.Once);

    [Fact]
    public async Task Va_al_correo_actual_con_lo_que_cambio_y_la_hora_de_servidor()
    {
        await Notifier().NotifyAsync(Usuario(), SecurityFactorChange.Phone);

        Enviado("actual@dominio.com", "en", v =>
            v["Change"] == "the phone number that receives your verification codes" &&
            v["ChangedAt"] == "2026-10-14 10:30");
    }

    [Fact]
    public async Task Al_cambiar_el_correo_va_al_anterior()
    {
        await Notifier().NotifyAsync(Usuario(), SecurityFactorChange.Email, toEmail: "anterior@dominio.com");

        Enviado("anterior@dominio.com", "en", v => v["Change"] == "the email address of the account");
        _email.Verify(e => e.SendAsync("actual@dominio.com", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sale_en_el_idioma_del_miembro_y_el_cambio_tambien()
    {
        _db.MemberProfiles.Add(new MemberProfile { MemberId = "AMB-000001", DefaultLanguage = "es", CreatedBy = "t" });
        await _db.SaveChangesAsync();

        await Notifier().NotifyAsync(Usuario("AMB-000001"), SecurityFactorChange.Authenticator);

        Enviado("actual@dominio.com", "es", v => v["Change"] == "la aplicación de autenticación vinculada a la cuenta");
    }

    [Fact]
    public async Task En_un_idioma_sin_plantilla_el_cambio_va_en_ingles_como_la_plantilla()
    {
        _db.MemberProfiles.Add(new MemberProfile { MemberId = "AMB-000002", DefaultLanguage = "pt", CreatedBy = "t" });
        await _db.SaveChangesAsync();

        await Notifier().NotifyAsync(Usuario("AMB-000002"), SecurityFactorChange.Channel);

        Enviado("actual@dominio.com", "pt", v => v["Change"] == "how you receive your verification codes");
    }

    [Fact]
    public async Task Un_fallo_del_transporte_no_se_propaga()
    {
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("SES caído"));

        var act = () => Notifier().NotifyAsync(Usuario(), SecurityFactorChange.Phone);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Sin_correo_no_intenta_enviar()
    {
        await Notifier().NotifyAsync(new ApplicationUser { Id = "user-001" }, SecurityFactorChange.Phone);

        _email.VerifyNoOtherCalls();
    }

    // ── aviso de bloqueo del segundo factor ──────────────────────────────────

    [Theory]
    [InlineData(false, "sign in")]
    [InlineData(true,  "confirm sensitive changes (such as where your commissions are paid)")]
    public async Task El_bloqueo_se_avisa_por_correo_con_lo_bloqueado_y_la_hora_de_servidor(bool stepUp, string action)
    {
        await Notifier().NotifyTwoFactorLockedAsync(Usuario(), stepUp);

        _email.Verify(e => e.SendAsync(
            "actual@dominio.com", It.IsAny<string>(), "en", NotificationEvents.TwoFactorLocked,
            It.Is<Dictionary<string, string>>(v => v["Action"] == action && v["LockedAt"] == "2026-10-14 10:30"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_aviso_de_bloqueo_sale_en_el_idioma_del_miembro()
    {
        _db.MemberProfiles.Add(new MemberProfile { MemberId = "AMB-000003", DefaultLanguage = "es", CreatedBy = "t" });
        await _db.SaveChangesAsync();

        await Notifier().NotifyTwoFactorLockedAsync(Usuario("AMB-000003"), stepUp: false);

        _email.Verify(e => e.SendAsync(
            "actual@dominio.com", It.IsAny<string>(), "es", NotificationEvents.TwoFactorLocked,
            It.Is<Dictionary<string, string>>(v => v["Action"] == "iniciar sesión"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_fallo_al_avisar_del_bloqueo_no_se_propaga()
    {
        _email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                  It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("SES caído"));

        var act = () => Notifier().NotifyTwoFactorLockedAsync(Usuario(), stepUp: true);

        await act.Should().NotThrowAsync();
    }
}
