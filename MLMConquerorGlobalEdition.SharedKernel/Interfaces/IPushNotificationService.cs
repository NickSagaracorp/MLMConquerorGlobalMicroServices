namespace MLMConquerorGlobalEdition.SharedKernel.Interfaces;

/// <summary>
/// Sends push notifications to members via Firebase Cloud Messaging.
/// Implementations must persist a MemberProfileNotificationTracking record.
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Sends a push notification to all devices registered for the given memberId.
    /// Fire-and-forget: implementations must never throw to callers.
    /// </summary>
    Task SendAsync(string memberId, string eventType, string title, string body,
        CancellationToken ct = default);
}
