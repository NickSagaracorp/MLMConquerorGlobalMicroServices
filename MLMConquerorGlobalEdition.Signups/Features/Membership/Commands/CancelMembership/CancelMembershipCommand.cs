using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Signups.Features.Membership.Commands.CancelMembership;

/// <summary>
/// Cancels a member's active subscription.
/// When <paramref name="ScheduledCancellationDate"/> is null the cancellation is immediate.
/// When a future date is provided the subscription stays active until that date;
/// a nightly job (ProcessScheduledCancellationsJob) then finalises it.
/// </summary>
public record CancelMembershipCommand(
    string MemberId,
    string? Reason,
    DateTime? ScheduledCancellationDate = null
) : IRequest<Result<bool>>;
