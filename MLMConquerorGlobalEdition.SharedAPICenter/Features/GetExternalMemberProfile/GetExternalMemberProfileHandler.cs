using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.GetExternalMemberProfile;

public class GetExternalMemberProfileHandler
    : IRequestHandler<GetExternalMemberProfileQuery, Result<ExternalMemberProfileDto>>
{
    private readonly AppDbContext _db;

    public GetExternalMemberProfileHandler(AppDbContext db) => _db = db;

    public async Task<Result<ExternalMemberProfileDto>> Handle(
        GetExternalMemberProfileQuery query, CancellationToken ct)
    {
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == query.MemberId, ct);

        if (member is null)
            return Result<ExternalMemberProfileDto>.Failure(
                "MEMBER_NOT_FOUND", $"Member '{query.MemberId}' not found.");

        return Result<ExternalMemberProfileDto>.Success(new ExternalMemberProfileDto
        {
            MemberId   = member.MemberId,
            FirstName  = member.FirstName,
            LastName   = member.LastName,
            MemberType = member.MemberType.ToString(),
            Status     = member.Status.ToString()
        });
    }
}
