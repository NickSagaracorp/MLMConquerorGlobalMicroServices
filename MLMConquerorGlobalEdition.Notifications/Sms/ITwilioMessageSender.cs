namespace MLMConquerorGlobalEdition.Notifications.Sms;

/// <summary>
/// Aísla la única llamada de red del envío por SMS, para que la resolución de plantilla y
/// la validación se puedan probar sin tocar Twilio.
/// </summary>
public interface ITwilioMessageSender
{
    Task SendAsync(string fromNumber, string toPhoneE164, string body, CancellationToken ct = default);
}
