using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Commissions;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Commissions.GetCommissionsBreakdown;

public class GetCommissionsBreakdownHandler : IRequestHandler<GetCommissionsBreakdownQuery, Result<List<CommissionBreakdownDto>>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetCommissionsBreakdownHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<List<CommissionBreakdownDto>>> Handle(GetCommissionsBreakdownQuery request, CancellationToken ct)
    {
        var memberId      = _currentUser.MemberId;
        var targetDateUtc = request.PaymentDate.Date;

        var raw = await _db.CommissionEarnings
            .AsNoTracking()
            .Where(c => c.BeneficiaryMemberId == memberId
                     && c.PaymentDate.Date == targetDateUtc
                     && (request.EarnedDate == null || c.EarnedDate.Date == request.EarnedDate.Value.Date))
            .Join(
                _db.CommissionTypes,
                c   => c.CommissionTypeId,
                ct2 => ct2.Id,
                (c, ct2) => new
                {
                    c.SourceMemberId,
                    c.SourceOrderId,
                    c.Notes,
                    c.Amount,
                    TypeName = ct2.Name,
                    TypeDesc = ct2.Description
                })
            .OrderBy(d => d.TypeName)
            .ToListAsync(ct);

        var sourceOrderIds = raw
            .Where(x => x.SourceOrderId != null)
            .Select(x => x.SourceOrderId!)
            .Distinct()
            .ToList();

        var orderNumbers = sourceOrderIds.Count > 0
            ? await _db.Orders
                .AsNoTracking()
                .Where(o => sourceOrderIds.Contains(o.Id))
                .Select(o => new { o.Id, Display = o.OrderNo ?? o.Id })
                .ToDictionaryAsync(o => o.Id, o => o.Display, ct)
            : new Dictionary<string, string>();

        var sourceIds = raw
            .Where(x => x.SourceMemberId != null)
            .Select(x => x.SourceMemberId!)
            .Distinct()
            .ToList();

        var memberNames = sourceIds.Count > 0
            ? await _db.MemberProfiles
                .AsNoTracking()
                .Where(mp => sourceIds.Contains(mp.MemberId))
                .Select(mp => new { mp.MemberId, FullName = mp.FirstName + " " + mp.LastName })
                .ToDictionaryAsync(mp => mp.MemberId, mp => mp.FullName, ct)
            : new Dictionary<string, string>();

        var items = raw.Select(x =>
        {
            string detail;
            if (!string.IsNullOrWhiteSpace(x.Notes))
            {
                // Notes stores human-readable detail (e.g. FSB "Member1 — Member2" format)
                detail = x.Notes;
            }
            else if (x.SourceOrderId != null)
            {
                var orderRef = orderNumbers.TryGetValue(x.SourceOrderId, out var no) ? no : x.SourceOrderId;
                var name = x.SourceMemberId != null && memberNames.TryGetValue(x.SourceMemberId, out var fullName)
                    ? $"{fullName} ({x.SourceMemberId})"
                    : x.SourceMemberId ?? string.Empty;
                detail = $"Order #{orderRef} — {name}";
            }
            else
            {
                detail = x.TypeDesc ?? string.Empty;
            }
            return new CommissionBreakdownDto
            {
                CommissionTypeName = x.TypeName,
                Detail             = detail,
                Amount             = x.Amount
            };
        }).ToList();

        return Result<List<CommissionBreakdownDto>>.Success(items);
    }
}
