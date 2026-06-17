using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.GenerateCertificateOnDemand;

/// <summary>
/// Validates that the rank history record belongs to the current member (defense
/// in depth — RankEngine also enforces ownership), then triggers cert generation
/// via the RankEngine HTTP client. Returns the generated certificate URL.
/// </summary>
public class GenerateCertificateOnDemandHandler
    : IRequestHandler<GenerateCertificateOnDemandCommand, Result<string>>
{
    private readonly AppDbContext         _db;
    private readonly ICurrentUserService  _currentUser;
    private readonly IRankEngineClient    _rankEngine;

    public GenerateCertificateOnDemandHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IRankEngineClient rankEngine)
    {
        _db          = db;
        _currentUser = currentUser;
        _rankEngine  = rankEngine;
    }

    public async Task<Result<string>> Handle(
        GenerateCertificateOnDemandCommand request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        // Ownership check — only the member can mint their own cert on demand.
        // Returning RANK_HISTORY_NOT_FOUND for both "missing" and "not owned by
        // caller" avoids leaking the existence of someone else's record.
        var owned = await _db.MemberRankHistories
            .AsNoTracking()
            .AnyAsync(h => h.Id == request.RankHistoryId
                           && h.MemberId == memberId
                           && !h.IsDeleted, ct);

        if (!owned)
            return Result<string>.Failure(
                "RANK_HISTORY_NOT_FOUND",
                "Rank history record not found.");

        return await _rankEngine.GenerateMemberCertificateAsync(
            request.RankHistoryId, request.BearerToken, ct);
    }
}
