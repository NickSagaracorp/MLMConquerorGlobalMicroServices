using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Payout;

public class PayoutHandler : IRequestHandler<PayoutCommand, Result<PayoutResponse>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public PayoutHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<PayoutResponse>> Handle(PayoutCommand command, CancellationToken ct)
    {
        var req = command.Request;
        var Now = _dateTime.Now;

        // Load pending commission earnings due today for the member
        var query = _db.CommissionEarnings
            .Where(e => e.BeneficiaryMemberId == req.MemberId
                        && e.Status == CommissionEarningStatus.Pending
                        && e.PaymentDate <= Now
                        && !e.IsDeleted);

        var earnings = await query
            .OrderBy(e => e.PaymentDate)
            .ToListAsync(ct);

        if (!earnings.Any())
            return Result<PayoutResponse>.Success(new PayoutResponse
            {
                MemberId = req.MemberId,
                EarningsPaid = 0,
                TotalPaid = 0m
            });

        // Apply MaxAmount cap if specified
        decimal runningTotal = 0m;
        var earningsToPay = new List<Domain.Entities.Commission.CommissionEarning>();
        foreach (var earning in earnings)
        {
            if (req.MaxAmount.HasValue && runningTotal + earning.Amount > req.MaxAmount.Value)
                break;

            earningsToPay.Add(earning);
            runningTotal += earning.Amount;
        }

        if (!earningsToPay.Any())
            return Result<PayoutResponse>.Success(new PayoutResponse
            {
                MemberId = req.MemberId,
                EarningsPaid = 0,
                TotalPaid = 0m
            });

        // Find or create member's eWallet
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.MemberId == req.MemberId
                                       && w.WalletType == WalletType.eWallet
                                       && !w.IsDeleted, ct);

        if (wallet is null)
        {
            wallet = new MemberProfilesWallet
            {
                MemberId = req.MemberId,
                WalletType = WalletType.eWallet,
                Status = WalletStatus.Approved,
                IsPreferred = false,
                IsDeleted = false,
                CreationDate = Now,
                CreatedBy = _currentUser.UserId,
                LastUpdateDate = Now,
                LastUpdateBy = _currentUser.UserId
            };
            _db.Wallets.Add(wallet);
            await _db.SaveChangesAsync(ct); // persist to get wallet Id
        }

        // Process each earning
        foreach (var earning in earningsToPay)
        {
            earning.Status = CommissionEarningStatus.Paid;
            earning.LastUpdateDate = Now;
            earning.LastUpdateBy = _currentUser.UserId;

            // Create wallet history credit entry
            var historyEntry = new MemberProfilesWalletHistory
            {
                WalletId = wallet.Id,
                MemberId = req.MemberId,
                WalletType = WalletType.eWallet,
                OldStatus = wallet.Status,
                NewStatus = wallet.Status,
                ChangeReason = $"COMMISSION_CREDIT:{earning.Amount:F2}:EarningId={earning.Id}",
                CreationDate = Now,
                CreatedBy = _currentUser.UserId
            };
            _db.WalletHistories.Add(historyEntry);
        }

        await _db.SaveChangesAsync(ct);

        return Result<PayoutResponse>.Success(new PayoutResponse
        {
            MemberId = req.MemberId,
            EarningsPaid = earningsToPay.Count,
            TotalPaid = runningTotal
        });
    }
}
