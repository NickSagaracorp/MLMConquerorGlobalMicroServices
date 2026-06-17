using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Ranks.DownloadCertificate;

public class DownloadCertificateHandler : IRequestHandler<DownloadCertificateQuery, Result<string>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IRankEngineClient   _rankEngine;

    public DownloadCertificateHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IRankEngineClient rankEngine)
    {
        _db          = db;
        _currentUser = currentUser;
        _rankEngine  = rankEngine;
    }

    public async Task<Result<string>> Handle(DownloadCertificateQuery request, CancellationToken ct)
    {
        var memberId = _currentUser.MemberId;

        var history = await _db.MemberRankHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.Id       == request.RankHistoryId &&
                h.MemberId == memberId &&
                !h.IsDeleted, ct);

        if (history is null)
            return Result<string>.Failure(
                "RANK_HISTORY_NOT_FOUND",
                "Rank history record not found.");

        // Lazy generation: certificates are no longer auto-minted during the rank
        // evaluation. If the URL is missing, trigger generation on demand (RankEngine
        // re-enforces ownership) — the bearer token from the caller's request is
        // relayed so RankEngine treats it as the same authenticated principal.
        if (history.GeneratedCertificateUrl is null)
        {
            if (string.IsNullOrEmpty(request.BearerToken))
                return Result<string>.Failure(
                    "AUTH_TOKEN_MISSING",
                    "Caller bearer token is required to generate the certificate.");

            var generated = await _rankEngine.GenerateMemberCertificateAsync(
                request.RankHistoryId, request.BearerToken, ct);

            if (!generated.IsSuccess)
                return generated;

            return Result<string>.Success(generated.Value!);
        }

        return Result<string>.Success(history.GeneratedCertificateUrl);
    }
}
