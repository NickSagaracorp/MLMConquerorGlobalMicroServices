using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
/// <remarks>
/// En SQL, cuatro consultas: las altas del rango cuyo patrocinador está en el equipo (Node de
/// genealogía del que mira, él incluido, por el nodo más reciente de cada patrocinador, R8), las
/// órdenes de membresía de esos inscritos con el nivel más alto de cada una (la regla de
/// <see cref="OrderMembershipLevelQueries"/>), y los nombres y puntos de los patrocinadores. En
/// memoria: la orden de alta de cada inscrito (la primera), el pivote por tipo y nivel, la
/// búsqueda, el orden y la página. El rango de actividad (3 meses hacia atrás como mucho, N4)
/// acota cuántas altas se traen (R5); los Contains van sobre esas filas ya materializadas (R16).
/// </remarks>
public sealed class EnrollmentActivityService : IEnrollmentActivityService
{
    public static readonly int[] LevelColumns = { 1, 2, 3, 4 };

    private static readonly string[] SearchableFields =
    {
        nameof(EnrollmentActivityRowView.MemberId),
        nameof(EnrollmentActivityRowView.FullName)
    };

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public EnrollmentActivityService(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<List<EnrollmentActivityColumnView>> GetColumnsAsync(CancellationToken ct = default)
    {
        var names = await _db.MembershipLevels.AsNoTracking()
            .Where(l => LevelColumns.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        return LevelColumns
            .Select(id => new EnrollmentActivityColumnView { LevelId = id, Name = names.GetValueOrDefault(id, $"#{id}") })
            .ToList();
    }

    public async Task<Result<PagedResult<EnrollmentActivityRowView>>> GetGridAsync(
        string memberId, DateTime from, DateTime to, GridDataRequest request, CancellationToken ct = default)
    {
        if (!ReportDateRange.TryCreateForActivity(from, to, _clock.Now, out var range, out var error))
            return Result<PagedResult<EnrollmentActivityRowView>>.Failure(error!, "Invalid date range.");

        // El nodo más reciente del que mira: un miembro puede tener dos (R8).
        var me = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => new { g.Node, g.Level })
            .FirstOrDefaultAsync(ct);
        if (me?.Node is null) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        var start  = range.Start;
        var end    = range.End;
        var root   = me.Node;

        // Las altas del rango cuyo patrocinador está en el equipo. La pertenencia del patrocinador
        // la decide su nodo MÁS RECIENTE (R8): un JOIN directo a GenealogyTree contaría dos veces a
        // quien tiene dos nodos y metería a quien se fue con un nodo obsoleto bajo el Node del que mira.
        // Se materializa UNA vez (R16): todo lo demás sale de estas filas, sin repetir la consulta.
        var rows = await (
                from m in _db.MemberProfiles.AsNoTracking()
                where m.EnrollDate >= start && m.EnrollDate < end && m.SponsorMemberId != null
                let sponsorNode = _db.GenealogyTree
                    .Where(g => g.MemberId == m.SponsorMemberId)
                    .OrderByDescending(g => g.CreationDate)
                    .Select(g => new { g.Node, g.Level })
                    .FirstOrDefault()
                where sponsorNode != null && sponsorNode.Node!.IsDescendantOf(root)
                select new
                {
                    m.MemberId,
                    SponsorMemberId = m.SponsorMemberId!,
                    m.MemberType,
                    SponsorLevel    = sponsorNode!.Level
                })
            .ToListAsync(ct);
        if (rows.Count == 0) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        var enrolledIds = rows.Select(r => r.MemberId).Distinct().ToList();
        var sponsorIds  = rows.Select(r => r.SponsorMemberId).Distinct().ToList();

        // Corrección de Nick N2: el nivel del inscrito lo da su ORDEN DE ALTA (la primera suya con
        // productos de membresía), el más alto de sus productos. Ni la suscripción ni una compra
        // posterior lo mueven de columna. No se filtra por estado del pedido: el alta ocurrió.
        // Una sola consulta (R16): las órdenes de membresía de los inscritos con el nivel más alto
        // de cada una, la misma regla que OrderMembershipLevelQueries (g.Max()); la primera de cada
        // miembro se elige en memoria.
        var membershipOrders = await (
                from o in _db.Orders.AsNoTracking()
                where enrolledIds.Contains(o.MemberId)
                join od in _db.OrderDetails.AsNoTracking() on o.Id equals od.OrderId
                join p in _db.Products.AsNoTracking() on od.ProductId equals p.Id
                where p.MembershipLevelId != null
                group p.MembershipLevelId!.Value by new { o.Id, o.MemberId, o.OrderDate, o.CreationDate } into g
                select new { g.Key.MemberId, g.Key.OrderDate, g.Key.CreationDate, LevelId = g.Max() })
            .ToListAsync(ct);

        var signupLevel = membershipOrders
            .GroupBy(o => o.MemberId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(o => o.OrderDate).ThenBy(o => o.CreationDate).First().LevelId);

        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(p => sponsorIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, FullName = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.FullName, ct);

        var points = (await _db.MemberStatistics.AsNoTracking()
                .Where(st => sponsorIds.Contains(st.MemberId))
                .Select(st => new { st.MemberId, st.EnrollmentPoints })
                .ToListAsync(ct))
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.First().EnrollmentPoints);

        var result = rows
            .GroupBy(r => r.SponsorMemberId)
            .Select(g =>
            {
                var row = new EnrollmentActivityRowView
                {
                    MemberId             = g.Key,
                    FullName             = names.GetValueOrDefault(g.Key, g.Key),
                    Level                = g.Min(x => x.SponsorLevel) - me.Level,
                    EnrollmentTeamPoints = points.GetValueOrDefault(g.Key)
                };

                foreach (var e in g)
                {
                    if (e.MemberType == MemberType.ExternalMember) row.Customers++;
                    else row.Ambassadors++;

                    switch (signupLevel.GetValueOrDefault(e.MemberId))
                    {
                        case 1:  row.Level1++;     break;
                        case 2:  row.Level2++;     break;
                        case 3:  row.Level3++;     break;
                        case 4:  row.Level4++;     break;
                        default: row.OtherLevel++; break;
                    }
                }

                return row;
            })
            .ToList();

        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentActivityRowView.Ambassadors), Direction = "desc" });

        return Result<PagedResult<EnrollmentActivityRowView>>.Success(result.ToGridResult(request, SearchableFields));
    }

    private static PagedResult<EnrollmentActivityRowView> Empty(GridDataRequest request) => new()
    {
        Page     = Math.Max(1, request.Page),
        PageSize = request.PageSize <= 0 ? 20 : request.PageSize
    };
}
