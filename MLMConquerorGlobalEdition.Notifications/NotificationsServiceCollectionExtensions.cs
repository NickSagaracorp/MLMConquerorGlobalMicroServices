using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MLMConquerorGlobalEdition.Notifications.Email;
using MLMConquerorGlobalEdition.Notifications.Sms;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Services;

namespace MLMConquerorGlobalEdition.Notifications;

public static class NotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Cablea el transporte de correo y SMS según configuración. El default de cada canal es
    /// "Null" a propósito: un despliegue sin configurar no debe intentar enviar por un
    /// transporte a medias (credenciales vacías, remitente sin configurar); debe registrar en
    /// el log lo que habría enviado y seguir funcionando.
    /// </summary>
    public static IServiceCollection AddNotifications(
        this IServiceCollection services, IConfiguration config)
    {
        var emailProvider = config["Notifications:Email:Provider"] ?? "Null";
        if (emailProvider.Equals("Ses", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, SesEmailSender>();
            services.AddScoped<IEmailService, SesEmailService>();
        }
        else if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            // El SMTP sin TLS ni credenciales es un buzón de captura, no un transporte. Si un
            // appsettings de desarrollo llegara a producción, los códigos de 2FA y los enlaces de
            // restablecer contraseña se entregarían en claro a un servidor que no es el de SES (o
            // a nadie). Se corta al arrancar, no al primer envío.
            var environment = EnvironmentNameOf(config);
            if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Notifications:Email:Provider=Smtp solo se admite en el entorno Development " +
                    $"(entorno actual: '{environment}'). Usa 'Ses' o deja el proveedor sin configurar.");

            // Desarrollo contra un buzón de captura (MailHog). Mismo armado que SES —plantilla,
            // idioma, remitente de Notifications:Email:Ses— y solo cambia el transporte.
            services.AddScoped<IEmailSender, SmtpEmailSender>();
            services.AddScoped<IEmailService, SesEmailService>();
        }
        else
        {
            services.AddTransient<IEmailService, NullEmailService>();
        }

        var smsProvider = config["Notifications:Sms:Provider"] ?? "Null";
        if (smsProvider.Equals("Twilio", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ITwilioMessageSender, TwilioMessageSender>();
            services.AddScoped<ISmsService, TwilioSmsService>();
        }
        else
        {
            services.AddTransient<ISmsService, NullSmsService>();
        }

        return services;
    }

    /// <summary>
    /// El entorno sale de la configuración y no de <c>IHostEnvironment</c>: <c>AddNotifications</c>
    /// lo llaman cinco anfitriones con la misma firma, y <c>WebApplicationBuilder</c> ya vuelca
    /// ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT y --environment en su <c>IConfiguration</c>.
    /// Sin ninguno, ASP.NET Core asume Production, y aquí también: fallar cerrado.
    /// </summary>
    private static string EnvironmentNameOf(IConfiguration config) =>
        FirstNonEmpty(config["ASPNETCORE_ENVIRONMENT"], config["DOTNET_ENVIRONMENT"], config["environment"])
        ?? "Production";

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
