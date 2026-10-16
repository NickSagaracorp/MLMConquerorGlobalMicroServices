using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Tokens;

/// <summary>
/// Las consultas de solo lectura de los tokens de UN miembro: las tres cifras del resumen, el
/// historial y los saldos por tipo. Las usan el BizCenter (el miembro, con su id del token) y AdminAPI
/// (la ficha del miembro en el panel, con el id de la ruta), para que la pantalla compartida enseñe lo
/// mismo en los dos sitios sin copiar las consultas.
/// </summary>
public static class MemberTokenQueries
{
    public sealed record SummaryCounts(int Total, int Used, int Available);

    /// <summary>
    /// Las tres cifras en UNA consulta, sobre las instancias canjeables (con código) que hoy son del
    /// miembro: se agrupa todo en un solo grupo y cada cifra es un COUNT con su condición. Sin
    /// instancias no hay grupo y la consulta no devuelve fila (de ahí los ceros de quien la usa).
    /// </summary>
    public static IQueryable<SummaryCounts> Counts(AppDbContext db, string? memberId, DateTime now) =>
        db.TokenTransactions.AsNoTracking()
            .Where(t => t.MemberId == memberId && t.ReferenceId != null)
            .GroupBy(_ => 1)
            .Select(g => new SummaryCounts(
                g.Count(),
                g.Count(t => t.Status == TokenInstanceStatus.Used),
                g.Count(t => (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
                             && (t.ExpiresAt == null || t.ExpiresAt > now))));

    /// <summary>Una fila del historial de tokens del miembro.</summary>
    public sealed class HistoryRow
    {
        public string   TokenTypeName    { get; set; } = string.Empty;
        public string   TransactionType  { get; set; } = string.Empty;
        public string?  TokenCode        { get; set; }
        public bool     IsUsed           { get; set; }
        public string?  UsedByMemberId   { get; set; }
        public string?  UsedByMemberName { get; set; }
        public int      Amount           { get; set; }
        public DateTime OccurredAt       { get; set; }
        public string?  Notes            { get; set; }
    }

    /// <summary>El historial del miembro, sin ordenar ni paginar (lo hace quien lo usa).</summary>
    public static IQueryable<HistoryRow> History(AppDbContext db, string? memberId) =>
        from t in db.TokenTransactions.AsNoTracking()
        where t.MemberId == memberId
        join tt in db.TokenTypes on t.TokenTypeId equals tt.Id
        join mp in db.MemberProfiles on t.UsedByMemberId equals mp.MemberId into usedByGroup
        from usedBy in usedByGroup.DefaultIfEmpty()
        select new HistoryRow
        {
            TokenTypeName    = tt.Name,
            TransactionType  = t.TransactionType.ToString(),
            TokenCode        = t.ReferenceId,
            IsUsed           = t.UsedByMemberId != null,
            UsedByMemberId   = t.UsedByMemberId,
            UsedByMemberName = usedBy != null ? usedBy.FirstName + " " + usedBy.LastName : null,
            Amount           = t.Quantity,
            OccurredAt       = t.CreationDate,
            Notes            = t.Notes
        };

    /// <summary>Una página del historial, de lo más reciente a lo más antiguo, con el total.</summary>
    public static async Task<(List<HistoryRow> Items, int TotalCount)> HistoryPageAsync(
        AppDbContext db, string? memberId, int page, int pageSize, CancellationToken ct)
    {
        var query = History(db, memberId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <summary>Un saldo por tipo de token.</summary>
    public sealed class BalanceRow
    {
        public int    TokenTypeId   { get; set; }
        public string TokenTypeName { get; set; } = string.Empty;
        public bool   IsGuestPass   { get; set; }
        public int    Balance       { get; set; }
    }

    /// <summary>Los saldos del miembro por tipo de token.</summary>
    public static IQueryable<BalanceRow> Balances(AppDbContext db, string? memberId) =>
        db.TokenBalances.AsNoTracking()
            .Where(tb => tb.MemberId == memberId)
            .Join(db.TokenTypes, tb => tb.TokenTypeId, tt => tt.Id, (tb, tt) => new BalanceRow
            {
                TokenTypeId   = tt.Id,
                TokenTypeName = tt.Name,
                IsGuestPass   = tt.IsGuestPass,
                Balance       = tb.Balance
            });
}
