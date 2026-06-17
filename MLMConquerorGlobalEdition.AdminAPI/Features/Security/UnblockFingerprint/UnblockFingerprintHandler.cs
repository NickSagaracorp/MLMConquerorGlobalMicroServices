using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.UnblockFingerprint;

public class UnblockFingerprintHandler : IRequestHandler<UnblockFingerprintCommand, Result<int>>
{
    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICurrentUserService _currentUser;

    public UnblockFingerprintHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(UnblockFingerprintCommand command, CancellationToken ct)
    {
        var req = command.Request;

        var query = _db.SignupRiskFingerprints
            .Where(x => !x.Cleared);

        if (!string.IsNullOrWhiteSpace(req.VisitorId))
            query = query.Where(x => x.VisitorId == req.VisitorId);

        if (!string.IsNullOrWhiteSpace(req.IpAddress))
            query = query.Where(x => x.IpAddress == req.IpAddress);

        var rows = await query.ToListAsync(ct);
        if (rows.Count == 0)
            return Result<int>.Success(0);

        var now = _dateTime.Now;
        foreach (var row in rows)
        {
            row.Cleared     = true;
            row.ClearedAt   = now;
            row.ClearedBy   = _currentUser.UserId;
            row.ClearReason = req.Reason;
        }

        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(rows.Count);
    }
}
