using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.BackfillFsbCountdowns;

public record BackfillFsbCountdownsCommand : IRequest<Result<BackfillFsbCountdownsResult>>;

public class BackfillFsbCountdownsResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Details { get; set; } = new();
}
