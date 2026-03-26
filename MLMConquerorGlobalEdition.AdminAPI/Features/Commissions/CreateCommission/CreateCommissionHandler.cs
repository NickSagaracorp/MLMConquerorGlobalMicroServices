using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.CreateCommission;

public class CreateCommissionHandler : IRequestHandler<CreateCommissionCommand, Result<AdminCommissionDto>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateCommissionHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AdminCommissionDto>> Handle(
        CreateCommissionCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        var commissionType = await _db.CommissionTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.CommissionTypeId, cancellationToken);

        if (commissionType is null)
            return Result<AdminCommissionDto>.Failure(
                "COMMISSION_TYPE_NOT_FOUND",
                $"Commission type '{req.CommissionTypeId}' not found.");

        var now = _dateTime.Now;
        var earning = new CommissionEarning
        {
            BeneficiaryMemberId = req.BeneficiaryMemberId,
            CommissionTypeId = req.CommissionTypeId,
            Amount = req.Amount,
            Status = CommissionEarningStatus.Pending,
            EarnedDate = now,
            PaymentDate = now.AddDays(commissionType.PaymentDelayDays),
            PeriodDate = req.PeriodDate,
            Notes = req.Notes,
            IsManualEntry = true,
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        await _db.CommissionEarnings.AddAsync(earning, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new AdminCommissionDto
        {
            Id = earning.Id,
            BeneficiaryMemberId = earning.BeneficiaryMemberId,
            CommissionTypeName = commissionType.Name,
            Amount = earning.Amount,
            Status = earning.Status.ToString(),
            EarnedDate = earning.EarnedDate,
            PaymentDate = earning.PaymentDate,
            IsManualEntry = true
        };

        return Result<AdminCommissionDto>.Success(dto);
    }
}
