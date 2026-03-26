using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberRank;

/// <summary>
/// Loads the most recent MemberRankHistory entry for the given member
/// (ordered descending by AchievedAt) and maps it to ExternalMemberRankDto.
///
/// If the member has never achieved a rank, the DTO is returned with null rank fields
/// rather than a Failure — allowing callers to distinguish "member found, no rank yet"
/// from "member not found".
/// </summary>
public class GetExternalMemberRankHandler
    : IRequestHandler<GetExternalMemberRankQuery, Result<ExternalMemberRankDto>>
{
    private readonly AppDbContext _db;

    public GetExternalMemberRankHandler(AppDbContext db) => _db = db;

    public async Task<Result<ExternalMemberRankDto>> Handle(
        GetExternalMemberRankQuery request,
        CancellationToken ct = default)
    {
        // Verify the member exists first so we can distinguish "no rank" from "no member"
        var memberExists = await _db.MemberProfiles
            .AsNoTracking()
            .AnyAsync(m => m.MemberId == request.MemberId && !m.IsDeleted, ct);

        if (!memberExists)
            return Result<ExternalMemberRankDto>.Failure(
                "MEMBER_NOT_FOUND",
                $"Member '{request.MemberId}' was not found.");

        // Fetch the latest rank history entry with its RankDefinition
        var latest = await _db.MemberRankHistories
            .AsNoTracking()
            .Include(r => r.RankDefinition)
            .Where(r => r.MemberId == request.MemberId)
            .OrderByDescending(r => r.AchievedAt)
            .FirstOrDefaultAsync(ct);

        var dto = new ExternalMemberRankDto
        {
            MemberId             = request.MemberId,
            CurrentRankName      = latest?.RankDefinition?.Name,
            CurrentRankSortOrder = latest?.RankDefinition?.SortOrder,
            AchievedAt           = latest?.AchievedAt
        };

        return Result<ExternalMemberRankDto>.Success(dto);
    }
}
