using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services;

/// <summary>
/// Firebase Cloud Messaging implementation of IPushNotificationService.
/// Registered as a Singleton; uses IServiceScopeFactory for scoped DB writes.
///
/// Configuration keys:
///   Firebase:CredentialsFilePath  — path to service-account JSON (optional; uses ADC if absent)
///   Firebase:ProjectId            — GCP project ID
/// </summary>
public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FirebasePushNotificationService> _logger;
    private readonly FirebaseMessaging? _messaging;

    public FirebasePushNotificationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<FirebasePushNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        try
        {
            if (FirebaseApp.DefaultInstance is null)
            {
                var credPath = configuration["Firebase:CredentialsFilePath"];
                var credential = string.IsNullOrWhiteSpace(credPath)
                    ? GoogleCredential.GetApplicationDefault()
                    : GoogleCredential.FromFile(credPath);

                FirebaseApp.Create(new AppOptions { Credential = credential });
            }
            _messaging = FirebaseMessaging.DefaultInstance;
        }
        catch (Exception ex)
        {
            // Firebase is unavailable (e.g. no credentials in dev). Log and continue.
            _logger.LogWarning(ex,
                "Firebase initialisation failed — push notifications are disabled.");
        }
    }

    public async Task SendAsync(
        string memberId, string eventType, string title, string body,
        CancellationToken ct = default)
    {
        string? firebaseMessageId = null;

        if (_messaging is not null)
        {
            try
            {
                // Look up FCM registration tokens for this member
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var tokens = await db.MemberFcmTokens
                    .AsNoTracking()
                    .Where(t => t.MemberId == memberId && t.IsActive)
                    .Select(t => t.Token)
                    .ToListAsync(ct);

                if (tokens.Count > 0)
                {
                    var multicast = new MulticastMessage
                    {
                        Tokens = tokens,
                        Notification = new Notification { Title = title, Body = body },
                        Data = new Dictionary<string, string>
                        {
                            ["eventType"] = eventType,
                            ["memberId"]  = memberId
                        }
                    };

                    var response = await _messaging.SendEachForMulticastAsync(multicast, ct);
                    firebaseMessageId = response.SuccessCount > 0
                        ? $"multicast:{response.SuccessCount}/{tokens.Count}"
                        : null;

                    _logger.LogInformation(
                        "FCM sent to {MemberId}: {EventType} — {Success}/{Total} delivered.",
                        memberId, eventType, response.SuccessCount, tokens.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "FCM send failed for member {MemberId}, event {EventType}.", memberId, eventType);
            }
        }

        // Always persist notification record (whether sent or not)
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.MemberNotifications.AddAsync(new MemberProfileNotificationTracking
            {
                MemberId          = memberId,
                EventType         = eventType,
                Title             = title,
                Body              = body,
                FirebaseMessageId = firebaseMessageId,
                IsDelivered       = firebaseMessageId is not null,
                DeliveredAt       = firebaseMessageId is not null ? DateTime.UtcNow : null,
                CreationDate      = DateTime.UtcNow,
                CreatedBy         = "system"
            }, ct);

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist notification record for member {MemberId}.", memberId);
        }
    }
}
