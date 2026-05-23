using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GetMemberCertificates;

public class GetMemberCertificatesHandler
    : IRequestHandler<GetMemberCertificatesQuery, Result<List<MemberCertificateDto>>>
{
    private readonly AppDbContext _db;

    public GetMemberCertificatesHandler(AppDbContext db) => _db = db;

    public async Task<Result<List<MemberCertificateDto>>> Handle(
        GetMemberCertificatesQuery query, CancellationToken ct)
    {
        var memberExists = await _db.MemberProfiles
            .AsNoTracking()
            .AnyAsync(m => m.MemberId == query.MemberId, ct);

        if (!memberExists)
            return Result<List<MemberCertificateDto>>.Failure(
                "MEMBER_NOT_FOUND", $"Member '{query.MemberId}' not found.");

        var histories = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(h => h.RankDefinition)
            .Where(h => h.MemberId == query.MemberId && !h.IsDeleted)
            .ToListAsync(ct);

        var dtos = histories
            .Where(h => h.RankDefinition is not null &&
                        CertificateRules.IsCertificateEligible(h.RankDefinition.SortOrder))
            .GroupBy(h => h.RankDefinitionId)
            .Select(g =>
            {
                var earliest = g.OrderBy(h => h.AchievedAt).ThenBy(h => h.Id).First();
                return new MemberCertificateDto
                {
                    MemberRankHistoryId = earliest.Id,
                    RankDefinitionId    = earliest.RankDefinitionId,
                    RankName            = earliest.RankDefinition!.Name,
                    SortOrder           = earliest.RankDefinition.SortOrder,
                    FirstAchievedAt     = earliest.AchievedAt,
                    CertificateUrl      = earliest.GeneratedCertificateUrl,
                    HasCertificate      = earliest.GeneratedCertificateUrl is not null
                };
            })
            .OrderBy(d => d.SortOrder)
            .ToList();

        return Result<List<MemberCertificateDto>>.Success(dtos);
    }
}
