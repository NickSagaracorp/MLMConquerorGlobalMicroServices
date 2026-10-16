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
/// En SQL: las altas del rango cuyo patrocinador está en el equipo (prefijo de genealogía del que
/// mira, él incluido), sus órdenes con productos de membresía y el nivel más alto de cada orden
/// (<see cref="OrderMembershipLevelQueries"/>), todo unido por JOIN y no por Contains. En memoria:
/// la orden de alta de cada inscrito (la primera), el pivote por tipo y nivel, la búsqueda, el
/// orden y la página. El rango de actividad (3 meses hacia atrás como mucho, N4) acota cuántas
/// altas se traen (R5).
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
            .Select(g => new { g.HierarchyPath, g.Level })
            .FirstOrDefaultAsync(ct);
        if (me is null) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        var start  = range.Start;
        var end    = range.End;
        var prefix = me.HierarchyPath;

        var enrolled =
            from m in _db.MemberProfiles.AsNoTracking()
            where m.EnrollDate >= start && m.EnrollDate < end && m.SponsorMemberId != null
            join s in _db.GenealogyTree.AsNoTracking() on m.SponsorMemberId equals s.MemberId
            where s.HierarchyPath.StartsWith(prefix)
            select new { m.MemberId, SponsorMemberId = m.SponsorMemberId!, m.MemberType, SponsorLevel = s.Level };

        var rows = (await enrolled.ToListAsync(ct)).DistinctBy(r => r.MemberId).ToList();
        if (rows.Count == 0) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        // Corrección de Nick N2: el nivel del inscrito lo da su ORDEN DE ALTA (la primera suya con
        // productos de membresía), el más alto de sus productos. Ni la suscripción ni una compra
        // posterior lo mueven de columna. No se filtra por estado del pedido: el alta ocurrió.
        var enrolledIds = enrolled.Select(e => e.MemberId).Distinct();
        var membershipOrders =
            from o in _db.Orders.AsNoTracking()
            join id in enrolledIds on o.MemberId equals id
            where _db.OrderDetails.Any(od => od.OrderId == o.Id
                    && _db.Products.Any(p => p.Id == od.ProductId && p.MembershipLevelId != null))
            select new { o.Id, o.MemberId, o.OrderDate, o.CreationDate };

        var levelByOrder = await _db.GetHighestMembershipLevelIdsAsync(membershipOrders.Select(o => o.Id), ct);
        var signupLevel = (await membershipOrders.ToListAsync(ct))
            .GroupBy(o => o.MemberId)
            .ToDictionary(
                g => g.Key,
                g => levelByOrder.GetValueOrDefault(
                    g.OrderBy(o => o.OrderDate).ThenBy(o => o.CreationDate).First().Id));

        var sponsorIds = enrolled.Select(e => e.SponsorMemberId).Distinct();

        var names = await (
                from p in _db.MemberProfiles.AsNoTracking()
                join id in sponsorIds on p.MemberId equals id
                select new { p.MemberId, FullName = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(x => x.MemberId, x => x.FullName, ct);

        var points = (await (
                from st in _db.MemberStatistics.AsNoTracking()
                join id in sponsorIds on st.MemberId equals id
                select new { st.MemberId, st.EnrollmentPoints })
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
