using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.RenewMembership;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire recurring job — Daily 6:00 AM UTC.
/// Renews all active subscriptions where:
///   - IsAutoRenew = true
///   - CancellationDate is null
///   - IsFree = false
///   - (StartDate + 30 days) &lt;= today (monthly renewal window elapsed)
/// </summary>
public class MembershipAutoRenewalJob
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<MembershipAutoRenewalJob> _logger;

    public MembershipAutoRenewalJob(
        IMediator mediator,
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<MembershipAutoRenewalJob> logger)
    {
        _mediator = mediator;
        _db = db;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var Now = _dateTime.Now;
        var renewalThreshold = Now.AddDays(-30);

        var subscriptions = await _db.MembershipSubscriptions
            .Where(s => s.SubscriptionStatus == MembershipStatus.Active
                        && s.IsAutoRenew
                        && s.CancellationDate == null
                        && !s.IsFree
                        && !s.IsDeleted
                        && s.StartDate <= renewalThreshold)
            .ToListAsync(ct);

        _logger.LogInformation(
            "MembershipAutoRenewalJob: found {Count} subscriptions due for renewal at {Now}.",
            subscriptions.Count, Now);

        foreach (var subscription in subscriptions)
        {
            try
            {
                var result = await _mediator.Send(new RenewMembershipCommand(new MembershipRenewalRequest
                {
                    MemberId = subscription.MemberId,
                    SubscriptionId = subscription.Id
                }), ct);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "MembershipAutoRenewalJob: renewed subscription {SubscriptionId} for member {MemberId}.",
                        subscription.Id, subscription.MemberId);
                }
                else
                {
                    _logger.LogWarning(
                        "MembershipAutoRenewalJob: renewal failed for subscription {SubscriptionId}, member {MemberId}. Error [{Code}]: {Message}",
                        subscription.Id, subscription.MemberId, result.ErrorCode, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "MembershipAutoRenewalJob: unhandled exception renewing subscription {SubscriptionId} for member {MemberId}.",
                    subscription.Id, subscription.MemberId);
            }
        }

        _logger.LogInformation("MembershipAutoRenewalJob: completed processing at {Now}.", _dateTime.Now);
    }
}
