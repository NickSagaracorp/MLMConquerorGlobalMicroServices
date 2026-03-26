using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Features.Payout;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Jobs;

/// <summary>
/// HangFire recurring job — Weekly Friday 8:00 AM UTC.
/// For each member with Pending commission earnings where PaymentDate &lt;= today,
/// dispatches a PayoutCommand to mark them paid and credit the eWallet.
/// </summary>
public class CommissionPayoutJob
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<CommissionPayoutJob> _logger;

    public CommissionPayoutJob(
        IMediator mediator,
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<CommissionPayoutJob> logger)
    {
        _mediator = mediator;
        _db = db;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var Now = _dateTime.Now;

        // Get distinct member IDs with pending earnings due today
        var memberIds = await _db.CommissionEarnings
            .Where(e => e.Status == CommissionEarningStatus.Pending
                        && e.PaymentDate <= Now
                        && !e.IsDeleted)
            .Select(e => e.BeneficiaryMemberId)
            .Distinct()
            .ToListAsync(ct);

        _logger.LogInformation(
            "CommissionPayoutJob: found {Count} members with pending earnings due at {Now}.",
            memberIds.Count, Now);

        foreach (var memberId in memberIds)
        {
            try
            {
                var result = await _mediator.Send(
                    new PayoutCommand(new PayoutRequest { MemberId = memberId }),
                    ct);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "CommissionPayoutJob: paid {EarningsPaid} earnings totalling {TotalPaid:F2} for member {MemberId}.",
                        result.Value!.EarningsPaid, result.Value.TotalPaid, memberId);
                }
                else
                {
                    _logger.LogWarning(
                        "CommissionPayoutJob: payout failed for member {MemberId}. Error [{Code}]: {Message}",
                        memberId, result.ErrorCode, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CommissionPayoutJob: unhandled exception processing payout for member {MemberId}.",
                    memberId);
            }
        }

        _logger.LogInformation("CommissionPayoutJob: completed at {Now}.", _dateTime.Now);
    }
}
