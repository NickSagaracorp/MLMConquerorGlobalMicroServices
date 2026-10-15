using System.Globalization;
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
    /// <summary>El UserName de i-Payout del miembro (AccountIdentifier de su eWallet aprobada), o null si no tiene.</summary>
    Task<string?> UserNameOfAsync(string memberId, CancellationToken ct = default);

    Task<Result<decimal>> BalanceAsync(string userName, CancellationToken ct = default);

    /// <summary>
    /// Cobra y verifica por saldo, como el legado. Nunca reintenta. El importe se redondea a centavos
    /// (ver <see cref="EWalletFunds.ChargeAmount"/>): quien compare saldos debe usar ese mismo importe.
    /// </summary>
    Task<EWalletDebitOutcome> DebitAsync(
        string userName, decimal amount, decimal balanceBefore, string reference, string description,
        CancellationToken ct = default);
}

public sealed class EWalletFunds : IEWalletFunds
{
    /// <summary>Holgura por redondeo de i-Payout.</summary>
    public const decimal Tolerance = 0.01m;

    /// <summary>Lo que cabe de detalle en las notas de la compra: un resumen, no la respuesta cruda.</summary>
    private const int DetailMaxLength = 200;

    private readonly AppDbContext   _db;
    private readonly IEWalletClient _client;

    public EWalletFunds(AppDbContext db, IEWalletClient client)
    {
        _db     = db;
        _client = client;
    }

    /// <summary>
    /// Lo que de verdad se pide a i-Payout: el importe a centavos, redondeando 0,005 hacia arriba. Los
    /// precios se guardan con 4 decimales y i-Payout trabaja en centavos; si se mandaran 4 decimales y
    /// se comparara el saldo con ellos, una diferencia de redondeo del gateway podría dejar a conciliar
    /// un cobro que sí se hizo.
    /// </summary>
    public static decimal ChargeAmount(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <remarks>
    /// EWalletPayoutGatewayService propone el MemberId como UserName al registrar la cuenta y guarda
    /// en AccountIdentifier lo que el gateway confirma o reasigna: el dato guardado es el bueno. Solo
    /// cuentan las wallets aprobadas y no borradas: una Pending o Rejected no es un método válido del
    /// miembro (el orquestador de pagos aplica el mismo filtro).
    /// </remarks>
    public Task<string?> UserNameOfAsync(string memberId, CancellationToken ct = default) =>
        _db.Wallets.AsNoTracking()
            .Where(w => w.MemberId == memberId
                     && w.WalletType == WalletType.eWallet
                     && w.Status == WalletStatus.Approved
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
        // Un solo importe para el cobro y para la verificación: si fueran distintos, la comparación de
        // saldos fallaría por centavos.
        var charge = ChargeAmount(amount);
        if (charge <= 0m)
            return new EWalletDebitOutcome(EWalletDebitStatus.Rejected, "EWALLET_INVALID_AMOUNT",
                $"Amount {amount.ToString(CultureInfo.InvariantCulture)} rounds to zero.");

        var deduction = await _client.DeductAsync(userName, charge, reference, description, ct);

        if (deduction.Status == EWalletDeductionStatus.Rejected)
            return new EWalletDebitOutcome(EWalletDebitStatus.Rejected, deduction.Code, Summary(deduction.Text));

        if (deduction.Status == EWalletDeductionStatus.Unknown)
            return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation,
                "EWALLET_OUTCOME_UNKNOWN", Summary($"{deduction.Code}: {deduction.Text}"));

        // Desde aquí el dinero pudo haber salido: nada cancela la verificación.
        var after = await _client.GetBalanceAsync(userName, "USD", CancellationToken.None);
        if (!after.IsSuccess)
            return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation,
                "EWALLET_BALANCE_UNVERIFIED", Summary($"Charge accepted; balance re-read failed ({after.ErrorCode})."));

        if (after.Value <= balanceBefore - charge + Tolerance)
            return new EWalletDebitOutcome(EWalletDebitStatus.Completed, null, Summary(deduction.Text));

        return new EWalletDebitOutcome(EWalletDebitStatus.NeedsReconciliation, "EWALLET_DEBIT_PENDING",
            Summary($"Accepted ({deduction.Text}) but balance went from {balanceBefore:F2} to {after.Value:F2}."));
    }

    /// <summary>
    /// Resumen corto y de una línea para las notas de la compra: lo que manda i-Payout se lee en un
    /// panel y no debe arrastrar cuerpos crudos ni saltos de línea.
    /// </summary>
    private static string? Summary(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var oneLine = new string(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return oneLine.Length <= DetailMaxLength ? oneLine : oneLine[..DetailMaxLength] + "…";
    }
}
