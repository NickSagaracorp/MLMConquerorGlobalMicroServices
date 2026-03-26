using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.RankRequirements.DeleteRankRequirement;

public class DeleteRankRequirementHandler
    : IRequestHandler<DeleteRankRequirementCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteRankRequirementHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(
        DeleteRankRequirementCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.RankRequirements
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.RankDefinitionId == request.RankId, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("RANK_REQUIREMENT_NOT_FOUND", "Rank requirement not found.");

        _db.RankRequirements.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
