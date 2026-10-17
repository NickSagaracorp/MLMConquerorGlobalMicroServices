using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.RenewMembership;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// APAGADO (Plan K, decisión de Nick): ya no se programa. El único motor de renovación es
/// RecurringBillingProcessor; con los dos, una suscripción podía cobrarse dos veces. Program.cs lo
/// borra de Hangfire (RemoveIfExists("membership-auto-renewal")). Se conserva registrado por si hay que
/// lanzarlo a mano en una emergencia, sabiendo que NO toma el candado de fondos ni es idempotente.
/// Lo que hacía: renovar toda suscripción activa con IsAutoRenew, sin CancellationDate, no gratuita y
/// con StartDate + 30 días &lt;= hoy.
/// </summary>
[Queue("billing")]
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
