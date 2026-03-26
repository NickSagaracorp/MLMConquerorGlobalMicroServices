using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services;

public class EWalletGatewayService : IGatewayService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public WalletType GatewayType => WalletType.eWallet;

    public EWalletGatewayService(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser)
    {
        _db = db;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<string>> ChargeAsync(
        string memberId,
        decimal amount,
        string currency,
        string description,
        CancellationToken ct = default)
    {
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.MemberId == memberId
                                      && w.WalletType == WalletType.eWallet
                                      && w.IsPreferred
                                      && !w.IsDeleted, ct);

        if (wallet is null)
            return Result<string>.Failure("EWALLET_NOT_FOUND", "No preferred eWallet found for member.");

        // eWallet balance is tracked via WalletHistory status changes;
        // for balance check we compute from history or use a balance field.
        // Since domain wallet does not store a decimal Balance field directly,
        // we use the wallet's AccountIdentifier as balance storage context.
        // We check available balance by summing credit/debit wallet history entries.
        var availableBalance = await ComputeBalanceAsync(wallet.Id, ct);

        if (availableBalance < amount)
            return Result<string>.Failure("INSUFFICIENT_BALANCE",
                $"Insufficient eWallet balance. Available: {availableBalance:F2}, Required: {amount:F2}");

        // Record debit as wallet history
        var historyEntry = new MemberProfilesWalletHistory
        {
            WalletId = wallet.Id,
            MemberId = memberId,
            WalletType = WalletType.eWallet,
            OldStatus = wallet.Status,
            NewStatus = wallet.Status,
            ChangeReason = $"DEBIT:{amount:F2}:{description}",
            CreationDate = _dateTime.Now,
            CreatedBy = _currentUser.UserId
        };

        _db.WalletHistories.Add(historyEntry);
        await _db.SaveChangesAsync(ct);

        return Result<string>.Success(wallet.Id);
    }

    public async Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default)
    {
        // gatewayTransactionId is the walletId
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.Id == gatewayTransactionId && !w.IsDeleted, ct);

        if (wallet is null)
            return Result<bool>.Failure("EWALLET_NOT_FOUND", $"eWallet with id '{gatewayTransactionId}' not found.");

        // Record credit refund as wallet history
        var historyEntry = new MemberProfilesWalletHistory
        {
            WalletId = wallet.Id,
            MemberId = wallet.MemberId,
            WalletType = WalletType.eWallet,
            OldStatus = wallet.Status,
            NewStatus = wallet.Status,
            ChangeReason = $"CREDIT_REFUND:{amount:F2}",
            CreationDate = _dateTime.Now,
            CreatedBy = _currentUser.UserId
        };

        _db.WalletHistories.Add(historyEntry);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Computes the current eWallet balance by parsing debit/credit entries from wallet history.
    /// DEBIT entries are prefixed with "DEBIT:" and CREDIT entries with "CREDIT:" or "CREDIT_REFUND:" or "COMMISSION_CREDIT:".
    /// </summary>
    private async Task<decimal> ComputeBalanceAsync(string walletId, CancellationToken ct)
    {
        var histories = await _db.WalletHistories
            .Where(h => h.WalletId == walletId)
            .Select(h => h.ChangeReason)
            .ToListAsync(ct);

        decimal balance = 0m;
        foreach (var reason in histories)
        {
            if (string.IsNullOrEmpty(reason)) continue;

            if (reason.StartsWith("DEBIT:", StringComparison.Ordinal))
            {
                var parts = reason.Split(':');
                if (parts.Length >= 2 && decimal.TryParse(parts[1], out var debit))
                    balance -= debit;
            }
            else if (reason.StartsWith("CREDIT:", StringComparison.Ordinal)
                     || reason.StartsWith("CREDIT_REFUND:", StringComparison.Ordinal)
                     || reason.StartsWith("COMMISSION_CREDIT:", StringComparison.Ordinal))
            {
                var parts = reason.Split(':');
                if (parts.Length >= 2 && decimal.TryParse(parts[1], out var credit))
                    balance += credit;
            }
        }

        return balance;
    }
}
