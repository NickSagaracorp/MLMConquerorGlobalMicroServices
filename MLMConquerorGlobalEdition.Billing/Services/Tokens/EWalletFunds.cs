using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Payout.EWallet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public enum EWalletDebitStatus { Completed, Rejected, NeedsReconciliation }

public sealed record EWalletDebitOutcome(EWalletDebitStatus Status, string? Code, string? Detail);

/// <summary>
/// El saldo REAL de i-Payout y el débito verificado. Sustituye, para las compras, al libro interno
/// de WalletHistory de EWalletGatewayService, que nadie acredita.
/// </summary>
public interface IEWalletFunds
{
    /// <summary>El UserName de i-Payout del miembro (AccountIdentifier de su eWallet), o null si no tiene.</summary>
    Task<string?> UserNameOfAsync(string memberId, CancellationToken ct = default);

    Task<Result<decimal>> BalanceAsync(string userName, CancellationToken ct = default);

    /// <summary>Cobra y verifica por saldo, como el legado. Nunca reintenta.</summary>
    Task<EWalletDebitOutcome> DebitAsync(
        string userName, decimal amount, decimal balanceBefore, string reference, string description,
        CancellationToken ct = default);
}

public sealed class EWalletFunds : IEWalletFunds
{
    /// <summary>Holgura por redondeo de i-Payout.</summary>
    private const decimal Tolerance = 0.01m;

    private readonly AppDbContext   _db;
    private readonly IEWalletClient _client;

    public EWalletFunds(AppDbContext db, IEWalletClient client)
    {
        _db     = db;
        _client = client;
    }

    /// <remarks>
    /// EWalletPayoutGatewayService propone el MemberId como UserName al registrar la cuenta y guarda
    /// en AccountIdentifier lo que el gateway confirma o reasigna: el dato guardado es el bueno.
    /// </remarks>
    public Task<string?> UserNameOfAsync(string memberId, CancellationToken ct = default) =>
        _db.Wallets.AsNoTracking()
            .Where(w => w.MemberId == memberId
                     && w.WalletType == WalletType.eWallet
                     && !w.IsDeleted
                     && w.AccountIdentifier != null
                     && w.AccountIdentifier != "")
            .OrderByDescending(w => w.IsPreferred)
            .Select(w => w.AccountIdentifier)
            .FirstOrDefaultAsync(ct);

    public Task<Result<decimal>> BalanceAsync(string userName, CancellationToken ct = default) =>
        _client.GetBalanceAsync(userName, "USD", ct);

    public async Task<EWalletDebitOutcome> DebitAsync(
        string userName, decimal amount, decimal balanceBefore, string reference, string description,
        CancellationToken ct = default)
    {
        var deduction = await _client.DeductAsync(userName, amount, reference, description, ct);

        if (deduction.Status == EWalletDeductionStatus.Rejected)
            return new EWalletDebitOutcome(EWalletDebitStatus.Rejected, deduction.Code, deduction.Text);

        if (deduction.Status == EWalletDeductionStatus.Unknown)
            return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation,
                "EWALLET_OUTCOME_UNKNOWN", $"{deduction.Code}: {deduction.Text}");

        // Desde aquí el dinero pudo haber salido: nada cancela la verificación.
        var after = await _client.GetBalanceAsync(userName, "USD", CancellationToken.None);
        if (!after.IsSuccess)
            return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation,
                "EWALLET_BALANCE_UNVERIFIED", after.Error);

        if (after.Value <= balanceBefore - amount + Tolerance)
            return new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, deduction.Text);

        return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING",
            $"Accepted ({deduction.Text}) but balance went from {balanceBefore:F2} to {after.Value:F2}.");
    }
}
