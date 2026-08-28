using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Notifications.Email;
using MLMConquerorGlobalEdition.Notifications.Sms;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Services;

namespace MLMConquerorGlobalEdition.Notifications.Tests;

/// <summary>
/// AddNotifications elige la implementación de cada canal según configuración. El default de
/// cada canal debe ser "Null" — un despliegue sin configurar no debe intentar enviar por un
/// transporte a medias.
/// </summary>
public class NotificationsServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddNotifications(config);

        return services.BuildServiceProvider();
    }

    // ── correo ───────────────────────────────────────────────────────────────

    [Fact]
    public void AddNotifications_WhenEmailProviderIsSes_ResolvesSesEmailService()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Notifications:Email:Provider"]        = "Ses",
            ["Notifications:Email:Ses:Region"]      = "us-east-1",
            ["Notifications:Email:Ses:FromAddress"] = "no-reply@mlmconqueror.com",
            ["Notifications:Email:Ses:FromName"]    = "MLM Conqueror"
        });

        using var scope = provider.CreateScope();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        email.Should().BeOfType<SesEmailService>();
    }

    [Fact]
    public void AddNotifications_WhenEmailProviderIsAbsent_ResolvesNullEmailService()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        using var scope = provider.CreateScope();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        email.Should().BeOfType<NullEmailService>();
    }

    [Fact]
    public void AddNotifications_WhenEmailProviderIsNull_ResolvesNullEmailService()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Notifications:Email:Provider"] = "Null"
        });

        using var scope = provider.CreateScope();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

        email.Should().BeOfType<NullEmailService>();
    }

    // ── SMS ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AddNotifications_WhenSmsProviderIsTwilio_ResolvesTwilioSmsService()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Notifications:Sms:Provider"]          = "Twilio",
            ["Notifications:Sms:Twilio:AccountSid"] = "ACfake",
            ["Notifications:Sms:Twilio:AuthToken"]  = "tokenfake",
            ["Notifications:Sms:Twilio:FromNumber"] = "+15005550006"
        });

        using var scope = provider.CreateScope();
        var sms = scope.ServiceProvider.GetRequiredService<ISmsService>();

        sms.Should().BeOfType<TwilioSmsService>();
    }

    [Fact]
    public void AddNotifications_WhenSmsProviderIsAbsent_ResolvesNullSmsService()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>());

        using var scope = provider.CreateScope();
        var sms = scope.ServiceProvider.GetRequiredService<ISmsService>();

        sms.Should().BeOfType<NullSmsService>();
    }

    [Fact]
    public void AddNotifications_WhenSmsProviderIsNull_ResolvesNullSmsService()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Notifications:Sms:Provider"] = "Null"
        });

        using var scope = provider.CreateScope();
        var sms = scope.ServiceProvider.GetRequiredService<ISmsService>();

        sms.Should().BeOfType<NullSmsService>();
    }
}
