using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CancelCommission;

public class CancelCommissionHandler : IRequestHandler<CancelCommissionCommand, Result<bool>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CancelCommissionHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(
        CancelCommissionCommand request, CancellationToken cancellationToken)
    {
        var earning = await _db.CommissionEarnings
            .FirstOrDefaultAsync(c => c.Id == request.CommissionId, cancellationToken);

        if (earning is null)
            return Result<bool>.Failure("COMMISSION_NOT_FOUND", $"Commission '{request.CommissionId}' not found.");

        if (earning.Status == CommissionEarningStatus.Paid)
            return Result<bool>.Failure("COMMISSION_ALREADY_PAID", "Cannot cancel a commission that has already been paid.");

        if (earning.Status == CommissionEarningStatus.Cancelled)
            return Result<bool>.Failure("COMMISSION_ALREADY_CANCELLED", "Commission is already cancelled.");

        earning.Status = CommissionEarningStatus.Cancelled;
        earning.LastUpdateDate = _dateTime.Now;
        earning.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
