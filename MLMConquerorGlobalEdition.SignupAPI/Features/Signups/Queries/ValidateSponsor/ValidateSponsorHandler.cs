using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.ValidateSponsor;

public class ValidateSponsorHandler : IRequestHandler<ValidateSponsorQuery, Result<SponsorInfoResponse>>
{
    private readonly AppDbContext _db;

    public ValidateSponsorHandler(AppDbContext db) => _db = db;

    public async Task<Result<SponsorInfoResponse>> Handle(ValidateSponsorQuery query, CancellationToken ct)
    {
        var sponsor = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MemberId == query.SponsorMemberId ||
                                      x.ReplicateSiteSlug == query.SponsorMemberId, ct);

        if (sponsor is null)
            return Result<SponsorInfoResponse>.Failure(
                "SPONSOR_NOT_FOUND", $"Sponsor '{query.SponsorMemberId}' not found.");

        var fullName = $"{sponsor.FirstName} {sponsor.LastName}".Trim();
        var displayName = sponsor.ShowBusinessName && !string.IsNullOrWhiteSpace(sponsor.BusinessName)
            ? sponsor.BusinessName
            : fullName;

        return Result<SponsorInfoResponse>.Success(new SponsorInfoResponse
        {
            MemberId = sponsor.MemberId,
            FullName = fullName,
            DisplayName = displayName,
            ReplicateSiteSlug = sponsor.ReplicateSiteSlug
        });
    }
}
