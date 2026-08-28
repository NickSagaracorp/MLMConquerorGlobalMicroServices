using Microsoft.Extensions.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace MLMConquerorGlobalEdition.Notifications.Sms;

/// <summary>
/// Real Twilio transport for <see cref="ITwilioMessageSender"/>. Initializes the Twilio SDK
/// client from configuration and sends the message via <c>MessageResource.CreateAsync</c>.
/// Never exercised in tests — those use a fake sender so the suite doesn't touch the network.
/// </summary>
public class TwilioMessageSender : ITwilioMessageSender
{
    public TwilioMessageSender(IConfiguration config)
    {
        var accountSid = config["Notifications:Sms:Twilio:AccountSid"]
            ?? throw new InvalidOperationException(
                "Missing configuration 'Notifications:Sms:Twilio:AccountSid'.");
        var authToken = config["Notifications:Sms:Twilio:AuthToken"]
            ?? throw new InvalidOperationException(
                "Missing configuration 'Notifications:Sms:Twilio:AuthToken'.");

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task SendAsync(string fromNumber, string toPhoneE164, string body, CancellationToken ct = default)
    {
        await MessageResource.CreateAsync(
            to: new PhoneNumber(toPhoneE164),
            from: new PhoneNumber(fromNumber),
            body: body);
    }
}
