using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.DeleteCertificate;

public class DeleteCertificateHandler : IRequestHandler<DeleteCertificateCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICertificateStorage _storage;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public DeleteCertificateHandler(
        AppDbContext db,
        ICertificateStorage storage,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _storage     = storage;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(DeleteCertificateCommand command, CancellationToken ct)
    {
        var history = await _db.MemberRankHistories
            .Include(h => h.RankDefinition)
            .FirstOrDefaultAsync(h => h.Id == command.MemberRankHistoryId && !h.IsDeleted, ct);

        if (history is null)
            return Result<bool>.Failure(
                "RANK_HISTORY_NOT_FOUND",
                $"Rank history record '{command.MemberRankHistoryId}' not found.");

        // The earliest record for this (member, rank) pair holds the certificate URL.
        var certRecord = await _db.MemberRankHistories
            .Where(h => h.MemberId == history.MemberId &&
                        h.RankDefinitionId == history.RankDefinitionId &&
                        !h.IsDeleted)
            .OrderBy(h => h.AchievedAt)
            .ThenBy(h => h.Id)
            .FirstAsync(ct);

        if (certRecord.GeneratedCertificateUrl is null)
            return Result<bool>.Failure(
                "CERTIFICATE_NOT_FOUND",
                "There is no certificate to delete for this rank achievement.");

        // The stored file name is the last segment of the URL.
        var fileName = certRecord.GeneratedCertificateUrl.Split('/').Last();
        await _storage.DeleteAsync(fileName, ct);

        certRecord.GeneratedCertificateUrl = null;
        certRecord.LastUpdateDate          = _dateTime.Now;
        certRecord.LastUpdateBy            = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
