using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.PayCommissions;

public class PayCommissionsHandler : IRequestHandler<PayCommissionsCommand, Result<int>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public PayCommissionsHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<int>> Handle(
        PayCommissionsCommand request, CancellationToken cancellationToken)
    {
        if (request.Request.CommissionIds is null || request.Request.CommissionIds.Count == 0)
            return Result<int>.Failure("NO_IDS_PROVIDED", "No commission IDs were provided.");

        var earnings = await _db.CommissionEarnings
            .Where(c => request.Request.CommissionIds.Contains(c.Id) &&
                        c.Status == CommissionEarningStatus.Pending)
            .ToListAsync(cancellationToken);

        var now = _dateTime.Now;
        foreach (var earning in earnings)
        {
            earning.Status = CommissionEarningStatus.Paid;
            earning.PaymentDate = now;
            earning.LastUpdateDate = now;
            earning.LastUpdateBy = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(earnings.Count);
    }
}
