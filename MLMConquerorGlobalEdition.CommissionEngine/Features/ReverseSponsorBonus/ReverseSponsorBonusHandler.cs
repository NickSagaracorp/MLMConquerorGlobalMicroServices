using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.ReverseSponsorBonus;

public class ReverseSponsorBonusHandler
    : IRequestHandler<ReverseSponsorBonusCommand, Result<CalculationResultResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public ReverseSponsorBonusHandler(
        AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        ReverseSponsorBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // ── Validate the 14-day window ────────────────────────────────────────
        var signupOrder = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == command.SignupOrderId, ct);

        if (signupOrder is null)
            return Result<CalculationResultResponse>.Failure(
                "ORDER_NOT_FOUND", $"Order '{command.SignupOrderId}' not found.");

        if ((now - signupOrder.OrderDate).TotalDays > 14)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "SponsorBonusReversal",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = now,
                SkippedReasons = new()
                {
                    $"Order '{command.SignupOrderId}' is outside the 14-day reversal window."
                }
            });

        // ── Find the original sponsor bonus earning ───────────────────────────
        var sponsorBonusTypeIds = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.IsSponsorBonus)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (sponsorBonusTypeIds.Count == 0)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "SponsorBonusReversal",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = now,
                SkippedReasons = new() { "No sponsor bonus commission types configured." }
            });

        var earning = await _db.CommissionEarnings
            .Include(e => e.CommissionType)
            .FirstOrDefaultAsync(e => e.SourceOrderId == command.SignupOrderId
                                   && e.SourceMemberId == command.CancelledMemberId
                                   && sponsorBonusTypeIds.Contains(e.CommissionTypeId), ct);

        if (earning is null)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType = "SponsorBonusReversal",
                RecordsCreated = 0,
                TotalAmountCalculated = 0,
                PeriodDate = now,
                SkippedReasons = new()
                {
                    $"No sponsor bonus earning found for order '{command.SignupOrderId}'."
                }
            });

        var note = command.Reason?.Trim().Length > 0
            ? command.Reason
            : "Cancellation within 14-day chargeback window.";

        int recordsCreated = 0;
        decimal totalAmount = 0;

        if (earning.Status == CommissionEarningStatus.Pending)
        {
            // Domain method cancels the pending earning in place
            earning.Cancel(note);
            earning.LastUpdateBy = _currentUser.UserId;
        }
        else if (earning.Status == CommissionEarningStatus.Paid)
        {
            var reverseCommTypeId = earning.CommissionType!.ReverseId;
            if (reverseCommTypeId == 0)
                return Result<CalculationResultResponse>.Failure(
                    "NO_REVERSE_TYPE",
                    $"Commission type '{earning.CommissionTypeId}' has no ReverseId configured.");

            var reverseType = await _db.CommissionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == reverseCommTypeId && t.IsActive, ct);

            if (reverseType is null)
                return Result<CalculationResultResponse>.Failure(
                    "REVERSE_TYPE_NOT_FOUND",
                    $"Reversal commission type '{reverseCommTypeId}' not found or inactive.");

            // Idempotency: skip if reversal already exists
            var reversalExists = await _db.CommissionEarnings
                .AnyAsync(e => e.SourceOrderId == command.SignupOrderId
                            && e.SourceMemberId == command.CancelledMemberId
                            && e.CommissionTypeId == reverseCommTypeId, ct);

            if (!reversalExists)
            {
                var reversal = new CommissionEarning
                {
                    BeneficiaryMemberId = earning.BeneficiaryMemberId,
                    SourceMemberId = command.CancelledMemberId,
                    SourceOrderId = command.SignupOrderId,
                    CommissionTypeId = reverseCommTypeId,
                    Amount = -earning.Amount,
                    Status = CommissionEarningStatus.Pending,
                    EarnedDate = now,
                    PaymentDate = now.AddDays(reverseType.PaymentDelayDays),
                    PeriodDate = now.Date,
                    Notes = $"Reversal of sponsor bonus (earning {earning.Id}) — {note}",
                    CreatedBy = _currentUser.UserId,
                    CreationDate = now,
                    LastUpdateDate = now
                };

                await _db.CommissionEarnings.AddAsync(reversal, ct);
                recordsCreated = 1;
                totalAmount = reversal.Amount;
            }
        }
        // Status = Cancelled → already reversed, no action needed

        await _db.SaveChangesAsync(ct);

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType = "SponsorBonusReversal",
            RecordsCreated = recordsCreated,
            TotalAmountCalculated = totalAmount,
            PeriodDate = now
        });
    }
}
