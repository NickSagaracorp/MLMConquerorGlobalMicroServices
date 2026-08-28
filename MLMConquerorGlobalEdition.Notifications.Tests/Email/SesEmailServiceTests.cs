using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Domain.Entities.Email;
using MLMConquerorGlobalEdition.Notifications.Email;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Notifications.Tests.Email;

public class SesEmailServiceTests
{
    private const string EventType   = "TWO_FACTOR_CODE";
    private const string FromAddress = "no-reply@mlmconqueror.com";
    private const string FromName    = "MLM Conqueror";
    private const string ToEmail     = "member@example.com";
    private const string ToName      = "Nick";

    private readonly Mock<IEmailSender> _sender = new();

    // ── andamiaje ────────────────────────────────────────────────────────────

    private static AppDbContext BuildDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static IConfiguration BuildConfig(
        string fromAddress = FromAddress, string fromName = FromName) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Email:Ses:Region"]      = "us-east-1",
            ["Notifications:Email:Ses:FromAddress"] = fromAddress,
            ["Notifications:Email:Ses:FromName"]    = fromName
        }).Build();

    private SesEmailService BuildService(AppDbContext db, IConfiguration? config = null) =>
        new(db, _sender.Object, config ?? BuildConfig());

    private static void SeedTemplate(
        AppDbContext db, string eventType, bool isActive,
        params (string Lang, string Subject, string HtmlBody, string? TextBody)[] localizations)
    {
        var template = new EmailTemplate
        {
            Name         = eventType,
            EventType    = eventType,
            Category     = "Auth",
            IsActive     = isActive,
            CreatedBy    = "test",
            CreationDate = DateTime.UtcNow
        };

        foreach (var (lang, subject, htmlBody, textBody) in localizations)
        {
            template.Localizations.Add(new EmailTemplateLocalization
            {
                LanguageCode = lang,
                Subject      = subject,
                HtmlBody     = htmlBody,
                TextBody     = textBody,
                CreatedBy    = "test",
                CreationDate = DateTime.UtcNow
            });
        }

        db.EmailTemplates.Add(template);
        db.SaveChanges();
    }

    // ── resolución de plantilla ──────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_LooksUpTemplateByEventTypeAndLanguage()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Your code", "<p>Your code is {{Code}}</p>", "Your code is {{Code}}"),
            ("es", "Tu código", "<p>Tu código es {{Code}}</p>", "Tu código es {{Code}}"));

        var service = BuildService(db);

        await service.SendAsync(
            ToEmail, ToName, "es", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        _sender.Verify(s => s.SendAsync(
            FromAddress, FromName, ToEmail, ToName,
            "Tu código", "<p>Tu código es 123456</p>", "Tu código es 123456",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_FallsBackToEnglish_WhenLanguageMissing()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Your code", "<p>Your code is {{Code}}</p>", "Your code is {{Code}}"),
            ("es", "Tu código", "<p>Tu código es {{Code}}</p>", "Tu código es {{Code}}"));

        var service = BuildService(db);

        await service.SendAsync(
            ToEmail, ToName, "fr", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        _sender.Verify(s => s.SendAsync(
            FromAddress, FromName, ToEmail, ToName,
            "Your code", "<p>Your code is 123456</p>", "Your code is 123456",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_SubstitutesVariables_InSubjectAndBody()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Hi {{Name}}, your code",
             "<p>Hi {{Name}}, your code is {{Code}} and expires in {{ExpiresInMinutes}} min</p>",
             "Hi {{Name}}, your code is {{Code}} and expires in {{ExpiresInMinutes}} min"));

        var service = BuildService(db);

        await service.SendAsync(ToEmail, ToName, "en", EventType, new Dictionary<string, string>
        {
            ["Name"]             = "Nick",
            ["Code"]             = "654321",
            ["ExpiresInMinutes"] = "5"
        });

        _sender.Verify(s => s.SendAsync(
            FromAddress, FromName, ToEmail, ToName,
            "Hi Nick, your code",
            "<p>Hi Nick, your code is 654321 and expires in 5 min</p>",
            "Hi Nick, your code is 654321 and expires in 5 min",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTemplateMissing_ThrowsNamingEventType()
    {
        using var db = BuildDb();
        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            ToEmail, ToName, "en", "SOME_MISSING_EVENT", new Dictionary<string, string>());

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("SOME_MISSING_EVENT");
    }

    [Fact]
    public async Task SendAsync_WhenTemplateInactive_Throws()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: false,
            ("en", "Your code", "<p>Your code is {{Code}}</p>", null));

        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            ToEmail, ToName, "en", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(EventType);
    }

    [Fact]
    public async Task SendAsync_WhenNeitherLanguageNorEnglishExists_Throws()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("es", "Tu código", "<p>Tu código es {{Code}}</p>", null));

        var service = BuildService(db);

        var act = async () => await service.SendAsync(
            ToEmail, ToName, "fr", EventType, new Dictionary<string, string> { ["Code"] = "123456" });

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(EventType);
    }

    // ── configuración ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_PassesConfiguredSender()
    {
        using var db = BuildDb();
        SeedTemplate(db, EventType, isActive: true,
            ("en", "Your code", "<p>Your code is {{Code}}</p>", null));

        const string customFromAddress = "alerts@mlmconqueror.com";
        const string customFromName    = "MLM Conqueror Alerts";
        var service = BuildService(db, BuildConfig(customFromAddress, customFromName));

        await service.SendAsync(
            ToEmail, ToName, "en", EventType, new Dictionary<string, string> { ["Code"] = "999999" });

        _sender.Verify(s => s.SendAsync(
            customFromAddress, customFromName, ToEmail, ToName,
            "Your code", "<p>Your code is 999999</p>", null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
