using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Queries.ValidateReplicateSite;

public class ValidateReplicateSiteHandler : IRequestHandler<ValidateReplicateSiteQuery, Result<bool>>
{
    private readonly AppDbContext _db;

    public ValidateReplicateSiteHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ValidateReplicateSiteQuery query, CancellationToken ct)
    {
        var exists = await _db.MemberProfiles.AnyAsync(x => x.ReplicateSiteSlug == query.Slug, ct);
        return exists
            ? Result<bool>.Failure("SLUG_TAKEN", $"The replicate site slug '{query.Slug}' is already taken.")
            : Result<bool>.Success(true);
    }
}
