using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenTypeCommissions.DeleteTokenTypeCommission;

public class DeleteTokenTypeCommissionHandler
    : IRequestHandler<DeleteTokenTypeCommissionCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public DeleteTokenTypeCommissionHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<bool>> Handle(
        DeleteTokenTypeCommissionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.TokenTypeCommissions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("TOKEN_TYPE_COMMISSION_NOT_FOUND", "Token type commission not found.");

        _db.TokenTypeCommissions.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
