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
}
