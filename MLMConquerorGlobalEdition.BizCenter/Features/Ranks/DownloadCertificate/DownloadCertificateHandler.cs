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

    public DownloadCertificateHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
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

        if (history.GeneratedCertificateUrl is null)
            return Result<string>.Failure(
                "CERTIFICATE_NOT_READY",
                "The certificate for this rank achievement has not been generated yet. " +
                "Please try again in a few moments.");

        return Result<string>.Success(history.GeneratedCertificateUrl);
    }
}
