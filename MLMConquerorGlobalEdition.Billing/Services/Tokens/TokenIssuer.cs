using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.Tokens;

public sealed record TokenIssue(
    string MemberId, int TokenTypeId, int Quantity, string PurchaseId, string? OrderNo, string Actor, DateTime Now);

public interface ITokenIssuer
{
    /// <summary>
    /// Añade una TokenTransaction por token y sube el TokenBalance. NO guarda: quien llama lo
    /// persiste en el mismo SaveChanges que el cobro.
    /// </summary>
    Task<IReadOnlyList<string>> IssueAsync(TokenIssue issue, CancellationToken ct = default);
}

/// <inheritdoc cref="ITokenIssuer"/>
/// <remarks>
/// Como GrantTokensHandler y DistributeTokenHandler, pero rellena OriginalOwnerMemberId (el cupo
/// cuenta por él) y descarta códigos que ya existen: TokenCodeGenerator puede repetir y el índice
/// único de ReferenceId haría fallar el guardado DESPUÉS de cobrar.
/// </remarks>
public sealed class TokenIssuer : ITokenIssuer
{
    private const int MaxRounds = 10;

    private readonly AppDbContext _db;

    public TokenIssuer(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> IssueAsync(TokenIssue issue, CancellationToken ct = default)
    {
        if (issue.Quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(issue), "Quantity must be positive.");

        var codes = await NewCodesAsync(issue.Quantity, ct);

        foreach (var code in codes)
        {
            _db.TokenTransactions.Add(new TokenTransaction
            {
                MemberId              = issue.MemberId,
                OriginalOwnerMemberId = issue.MemberId,
                TokenTypeId           = issue.TokenTypeId,
                TransactionType       = TokenTransactionType.Purchased,
                Quantity              = 1,
                ReferenceId           = code,
                Status                = TokenInstanceStatus.Issued,
                TokenPurchaseId       = issue.PurchaseId,
                Notes                 = $"Purchased — order {issue.OrderNo}",
                CreatedBy             = issue.Actor,
                CreationDate          = issue.Now
            });
        }

        var balance = _db.TokenBalances.Local
                          .FirstOrDefault(b => b.MemberId == issue.MemberId && b.TokenTypeId == issue.TokenTypeId)
                      ?? await _db.TokenBalances
                          .FirstOrDefaultAsync(b => b.MemberId == issue.MemberId && b.TokenTypeId == issue.TokenTypeId, ct);

        if (balance is null)
        {
            balance = new TokenBalance
            {
                MemberId = issue.MemberId, TokenTypeId = issue.TokenTypeId, Balance = 0,
                CreatedBy = issue.Actor, CreationDate = issue.Now, LastUpdateDate = issue.Now
            };
            _db.TokenBalances.Add(balance);
        }

        balance.Add(issue.Quantity);
        balance.LastUpdateDate = issue.Now;
        balance.LastUpdateBy   = issue.Actor;

        return codes;
    }

    private async Task<List<string>> NewCodesAsync(int quantity, CancellationToken ct)
    {
        var chosen = new List<string>(quantity);

        for (var round = 0; round < MaxRounds && chosen.Count < quantity; round++)
        {
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            while (candidates.Count < quantity - chosen.Count)
            {
                var code = TokenCodeGenerator.Generate();
                if (!chosen.Contains(code)) candidates.Add(code);
            }

            var batch = candidates.ToList();
            var taken = await _db.TokenTransactions.AsNoTracking()
                .Where(t => t.ReferenceId != null && batch.Contains(t.ReferenceId))
                .Select(t => t.ReferenceId!)
                .ToListAsync(ct);

            chosen.AddRange(batch.Except(taken, StringComparer.Ordinal));
        }

        if (chosen.Count < quantity)
            throw new InvalidOperationException("Could not generate unique token codes.");

        return chosen;
    }
}
