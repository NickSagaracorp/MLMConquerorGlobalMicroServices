using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Queries;

/// <summary>El rango de un miembro con su nombre y su orden (de RankDefinitions) y la fecha de la fila elegida.</summary>
public sealed record MemberRankEntry(string MemberId, int RankDefinitionId, string RankName, int SortOrder, DateTime AchievedAt);

/// <summary>
/// Plan Q (visualizador del admin): el unico sitio que decide cual es el rango vigente de un miembro (la fila de su
/// historial con el AchievedAt mas reciente) y cual su rango maximo (la de mayor SortOrder). Antes, siete sitios lo
/// repetian: unos con GroupBy(...).First() en SQL (dos recorridos de la tabla, ROW_NUMBER y una cola de trabajo, sin
/// indice y con MemberId nvarchar(max)) y otros trayendo el historial entero a memoria.
///
/// Empate: dos filas del mismo miembro con el mismo AchievedAt (o el mismo SortOrder) se desempatan por el Id menor. Es
/// lo que hacian los sitios que ordenaban en memoria (OrderByDescending es estable y la tabla llegaba en el orden de su
/// clave); los de SQL no fijaban ninguna. El indice IX_MemberRankHistories_MemberId_AchievedAt lleva el Id como ultima
/// clave ascendente, asi que el desempate no ordena nada.
///
/// La forma es una antijoin (no hay otra fila del miembro que gane), no un GroupBy: con el indice, una busqueda por
/// miembro. La lista de ids va en un solo parametro (OPENJSON, EF.Parameter), no en uno por id.
/// </summary>
public static class MemberRankQueries
{
    /// <summary>Las filas que son el rango vigente de su miembro: una por miembro con historial.</summary>
    public static IQueryable<MemberRankHistory> CurrentRankRows(this AppDbContext db) =>
        db.MemberRankHistories.AsNoTracking()
            .Where(r => !db.MemberRankHistories.Any(o =>
                o.MemberId == r.MemberId
                && (o.AchievedAt > r.AchievedAt
                    || (o.AchievedAt == r.AchievedAt && string.Compare(o.Id, r.Id) < 0))));

    /// <summary>Rango vigente de cada miembro de <paramref name="memberIds"/> que tenga historial.</summary>
    public static async Task<Dictionary<string, MemberRankEntry>> GetCurrentRanksAsync(
        this AppDbContext db, IEnumerable<string> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, MemberRankEntry>();
        return await db.CurrentRanks(ids).ToDictionaryAsync(e => e.MemberId, ct);
    }

    /// <summary>La consulta de <see cref="GetCurrentRanksAsync"/> (sin quitar duplicados; expuesta para comprobar su plan).</summary>
    /// <remarks>
    /// Para una lista, la fila de cada miembro sale de una busqueda TOP(1) en el indice, en el mismo orden que la antijoin
    /// de <see cref="CurrentRankRows"/> (AchievedAt descendente y luego Id): con los 111 miembros del visualizador, 223
    /// lecturas frente a 1.095 de la antijoin, que mira cada fila del historial de cada miembro.
    /// </remarks>
    public static IQueryable<MemberRankEntry> CurrentRanks(this AppDbContext db, List<string> ids) =>
        from mid in db.MemberRankHistories.AsNoTracking()
            .Where(r => EF.Parameter(ids).Contains(r.MemberId)).Select(r => r.MemberId).Distinct()
        let top = db.MemberRankHistories
            .Where(m => m.MemberId == mid)
            .OrderByDescending(m => m.AchievedAt).ThenBy(m => m.Id)
            .Select(m => new { m.RankDefinitionId, m.AchievedAt })
            .First()
        join d in db.RankDefinitions.AsNoTracking() on top.RankDefinitionId equals d.Id
        select new MemberRankEntry(mid, top.RankDefinitionId, d.Name, d.SortOrder, top.AchievedAt);

    /// <summary>Rango vigente de un miembro, o null si no tiene historial.</summary>
    public static Task<MemberRankEntry?> GetCurrentRankAsync(this AppDbContext db, string memberId, CancellationToken ct = default) =>
        (from r in db.CurrentRankRows()
         where r.MemberId == memberId
         join d in db.RankDefinitions.AsNoTracking() on r.RankDefinitionId equals d.Id
         select new MemberRankEntry(r.MemberId, r.RankDefinitionId, d.Name, d.SortOrder, r.AchievedAt)
        ).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Rango maximo (de por vida) de cada miembro de <paramref name="memberIds"/>: la fila de mayor SortOrder, con el
    /// mismo desempate por Id.
    /// </summary>
    public static async Task<Dictionary<string, MemberRankEntry>> GetLifetimeRanksAsync(
        this AppDbContext db, IEnumerable<string> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, MemberRankEntry>();
        return await db.LifetimeRanks(ids).ToDictionaryAsync(e => e.MemberId, ct);
    }

    /// <summary>La consulta de <see cref="GetLifetimeRanksAsync"/> (sin quitar duplicados; expuesta para comprobar su plan).</summary>
    public static IQueryable<MemberRankEntry> LifetimeRanks(this AppDbContext db, List<string> ids) =>
        from r in db.MemberRankHistories.AsNoTracking()
        where EF.Parameter(ids).Contains(r.MemberId)
        join d in db.RankDefinitions.AsNoTracking() on r.RankDefinitionId equals d.Id
        where !(from o in db.MemberRankHistories
                join od in db.RankDefinitions on o.RankDefinitionId equals od.Id
                where o.MemberId == r.MemberId
                   && (od.SortOrder > d.SortOrder || (od.SortOrder == d.SortOrder && string.Compare(o.Id, r.Id) < 0))
                select o.Id).Any()
        select new MemberRankEntry(r.MemberId, r.RankDefinitionId, d.Name, d.SortOrder, r.AchievedAt);
}
