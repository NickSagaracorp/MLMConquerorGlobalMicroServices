using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.ResetFsbCountdowns;

/// <summary>
/// One-shot reset of every eligible ambassador's FSB countdown anchored on
/// the activation moment of a CorporatePromo. Idempotent — once
/// <c>ResetFsbCountdownExecutedAt</c> is set on the promo, a re-trigger
/// returns ALREADY_EXECUTED so admins can't accidentally hand out double
/// extensions.
/// </summary>
public record ResetFsbCountdownsCommand(string PromoId) : IRequest<Result<ResetFsbCountdownsResponse>>;

public class ResetFsbCountdownsResponse
{
    public string   PromoId             { get; set; } = string.Empty;
    public int      AmbassadorsReset    { get; set; }
    public int      ArchivedRows        { get; set; }
    public DateTime ExecutedAt          { get; set; }
}
