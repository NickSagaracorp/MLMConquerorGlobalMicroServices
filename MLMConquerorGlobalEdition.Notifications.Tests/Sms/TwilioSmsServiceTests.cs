using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Domain.Entities.Sms;
using MLMConquerorGlobalEdition.Notifications.Sms;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Notifications.Tests.Sms;

public class TwilioSmsServiceTests
{
    private const string EventType  = "TWO_FACTOR_CODE";
    private const string FromNumber = "+15005550006";
    private const string ToPhone    = "+14155552671";

    private readonly Mock<ITwilioMessageSender> _sender = new();

    // ── andamiaje ────────────────────────────────────────────────────────────

    private static AppDbContext BuildDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfig(string fromNumber = FromNumber) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Sms:Twilio:AccountSid"]   = "ACfake",
            ["Notifications:Sms:Twilio:AuthToken"]    = "tokenfake",
            ["Notifications:Sms:Twilio:FromNumber"]   = fromNumber
        }).Build();

    private TwilioSmsService BuildService(AppDbContext db, IConfiguration? config = null) =>
        new(db, _sender.Object, config ?? BuildConfig());

    private static void SeedTemplate(
        AppDbContext db, string eventType, bool isActive, params (string Lang, string Body)[] localizations)
    {
        var template = new SmsTemplate
        {
            Name       = eventType,
            EventType  = eventType,
            IsActive   = isActive,
            CreatedBy  = "test",
            CreationDate = DateTime.UtcNow
        };

        foreach (var (lang, body) in localizations)
        {
            template.Localizations.Add(new SmsTemplateLocalization
            {
                LanguageCode = lang,
                Body         = body,
                CreatedBy    = "test",
                CreationDate = DateTime.UtcNow
            });
        }

        db.SmsTemplates.Add(template);
        db.SaveChanges();
    }

    // ── resolución de plantilla ──────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_LooksUpTemplateByEventTypeAndLanguage()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Your code is {{Code}}"),
            ("es", "Tu código es {{Code}}"));

        var service = BuildService(db);

        await service.SendAsync(ToPhone, "es", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        _sender.Verify(s => s.SendAsync(
            FromNumber, ToPhone, "Tu código es 123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_FallsBackToEnglish_WhenLanguageMissing()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Your code is {{Code}}"),
            ("es", "Tu código es {{Code}}"));

        var service = BuildService(db);

        await service.SendAsync(ToPhone, "fr", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        _sender.Verify(s => s.SendAsync(
            FromNumber, ToPhone, "Your code is 123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_SubstitutesVariables()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Hi {{Name}}, your code is {{Code}} and expires in {{ExpiresInMinutes}} min"));

        var service = BuildService(db);

        await service.SendAsync(ToPhone, "en", EventType, new Dictionary<string, string>
        {
            ["Name"]             = "Nick",
            ["Code"]             = "654321",
            ["ExpiresInMinutes"] = "5"
        });

        _sender.Verify(s => s.SendAsync(
            FromNumber, ToPhone, "Hi Nick, your code is 654321 and expires in 5 min",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTemplateMissing_ThrowsNamingEventType()
    {
        using var db = BuildDb();
        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            ToPhone, "en", "SOME_MISSING_EVENT", new Dictionary<string, string>());

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("SOME_MISSING_EVENT");
    }

    [Fact]
    public async Task SendAsync_WhenTemplateInactive_Throws()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: false, ("en", "Your code is {{Code}}"));

        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            ToPhone, "en", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(EventType);
    }

    [Fact]
    public async Task SendAsync_WhenNeitherLanguageNorEnglishExists_Throws()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true, ("es", "Tu código es {{Code}}"));

        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            ToPhone, "fr", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(EventType);
    }

    // ── validación del teléfono ──────────────────────────────────────────────

    [Theory]
    [InlineData("14155552671")]     // sin '+'
    [InlineData("+1415555")]        // demasiado corto (menos de 8 dígitos)
    [InlineData("+1234567890123456")] // demasiado largo (más de 15 dígitos)
    [InlineData("+1 415 555 2671")] // espacios
    [InlineData("+1-415-555-2671")] // guiones
    [InlineData("")]
    public async Task SendAsync_WhenPhoneNotE164_Throws(string badPhone)
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true, ("en", "Your code is {{Code}}"));

        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            badPhone, "en", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        await act.Should().ThrowAsync<ArgumentException>();

        _sender.Verify(s => s.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── configuración ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_PassesConfiguredFromNumber()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true, ("en", "Your code is {{Code}}"));

        const string customFrom = "+15005550099";
        var service = BuildService(db, BuildConfig(customFrom));

        await service.SendAsync(ToPhone, "en", EventType, new Dictionary<string, string> { ["Code"] = "999999" });

        _sender.Verify(s => s.SendAsync(
            customFrom, ToPhone, "Your code is 999999", It.IsAny<CancellationToken>()), Times.Once);
    }
}
