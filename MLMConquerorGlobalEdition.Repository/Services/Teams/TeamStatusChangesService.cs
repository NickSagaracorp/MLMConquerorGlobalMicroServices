using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Teams;

/// <inheritdoc />
/// <remarks>
/// En SQL: el UNION de los MemberId con cambios en el rango (los dos historiales, por su índice de
/// ChangedAt), cruzado con el alcance por el nodo MÁS RECIENTE de cada miembro (R8) y proyectado a
/// columnas de MemberProfiles, con búsqueda, filtro, orden, cuenta y página. Por página: suscripción
/// actual y su nivel por la orden (N2), últimas fechas, «Calificado» (IQualificationService, N3),
/// nombres y pierna.
/// </remarks>
public sealed class TeamStatusChangesService : ITeamStatusChangesService
{
    private static readonly string[] DbFields =
    {
        nameof(TeamStatusChangeView.MemberId),
        nameof(TeamStatusChangeView.FirstName),
        nameof(TeamStatusChangeView.LastName),
        nameof(TeamStatusChangeView.EnrollDate),
        nameof(TeamStatusChangeView.AccountStatus),
        nameof(TeamStatusChangeView.SponsorMemberId)
    };

    private static readonly string[] SearchableFields =
    {
        nameof(TeamStatusChangeView.MemberId),
        nameof(TeamStatusChangeView.FirstName),
        nameof(TeamStatusChangeView.LastName)
    };

    private readonly AppDbContext          _db;
    private readonly IQualificationService _qualification;

    public TeamStatusChangesService(AppDbContext db, IQualificationService qualification)
    {
        _db            = db;
        _qualification = qualification;
    }

    public async Task<Result<PagedResult<TeamStatusChangeView>>> GetGridAsync(
        string memberId, StatusChangesScope scope, DateTime from, DateTime to,
        GridDataRequest request, CancellationToken ct = default)
    {
        if (!ReportDateRange.TryCreate(from, to, out var range, out var error))
            return Result<PagedResult<TeamStatusChangeView>>.Failure(error!, "Invalid date range.");

        Sanitize(request);

        var start = range.Start;
        var end   = range.End;

        // Primero se reduce por fecha (J17): solo quien tiene un cambio registrado en el rango.
        var changedIds = _db.MemberStatusHistories.AsNoTracking()
            .Where(h => h.ChangedAt >= start && h.ChangedAt < end)
            .Select(h => h.MemberId)
            .Union(_db.MembershipStatusHistories.AsNoTracking()
                .Where(h => h.ChangedAt >= start && h.ChangedAt < end)
                .Select(h => h.MemberId));

        var query = scope == StatusChangesScope.DualTeam
            ? await DualTeamQueryAsync(memberId, changedIds, ct)
            : await EnrollmentQueryAsync(memberId, changedIds, scope == StatusChangesScope.Customers, ct);

        if (query is null)
            return Result<PagedResult<TeamStatusChangeView>>.Success(new PagedResult<TeamStatusChangeView>
            {
                Page = Math.Max(1, request.Page), PageSize = request.PageSize <= 0 ? 20 : request.PageSize
            });

        var page = await query.ToGridResultAsync(request, SearchableFields, ct: ct);
        var rows = page.Items as List<TeamStatusChangeView> ?? page.Items.ToList();
        await EnrichAsync(rows, memberId, scope, ct);
        page.Items = rows;
        return Result<PagedResult<TeamStatusChangeView>>.Success(page);
    }

    /// <summary>
    /// Inscripción y clientes: el prefijo es la ruta del nodo más reciente del que mira, y cada
    /// miembro con cambios pertenece al equipo si SU nodo más reciente cuelga de ese prefijo (R8).
    /// </summary>
    private async Task<IQueryable<TeamStatusChangeView>?> EnrollmentQueryAsync(
        string memberId, IQueryable<string> changedIds, bool customersOnly, CancellationToken ct)
    {
        var root = await _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);
        if (root is null) return null;

        return
            from id in changedIds
            join m in _db.MemberProfiles.AsNoTracking() on id equals m.MemberId
            where m.MemberId != memberId && (!customersOnly || m.MemberType == MemberType.ExternalMember)
            let node = _db.GenealogyTree
                .Where(g => g.MemberId == m.MemberId)
                .OrderByDescending(g => g.CreationDate)
                .Select(g => g.Node)
                .FirstOrDefault()
            where node != null && node.IsDescendantOf(root)
            select new TeamStatusChangeView
            {
                MemberId        = m.MemberId,
                FirstName       = m.FirstName,
                LastName        = m.LastName,
                EnrollDate      = m.EnrollDate,
                AccountStatus   = m.Status.ToString(),
                SponsorMemberId = m.SponsorMemberId
            };
    }

    /// <summary>
    /// Dual Team: los MemberId del rango se cruzan con su nodo binario más reciente y solo sobre
    /// esas filas se comprueba el prefijo binario, que no es indexable (J17).
    /// </summary>
    private async Task<IQueryable<TeamStatusChangeView>?> DualTeamQueryAsync(
        string memberId, IQueryable<string> changedIds, CancellationToken ct)
    {
        var path = await _db.DualTeamTree.AsNoTracking()
            .Where(d => d.MemberId == memberId)
            .OrderByDescending(d => d.CreationDate)
            .Select(d => d.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(path)) return null;
        var prefix = TreePrefix.Of(path);

        return
            from id in changedIds
            join m in _db.MemberProfiles.AsNoTracking() on id equals m.MemberId
            where m.MemberId != memberId
            let dualPath = _db.DualTeamTree
                .Where(d => d.MemberId == m.MemberId)
                .OrderByDescending(d => d.CreationDate)
                .Select(d => d.HierarchyPath)
                .FirstOrDefault()
            where dualPath != null && dualPath.StartsWith(prefix)
            select new TeamStatusChangeView
            {
                MemberId        = m.MemberId,
                FirstName       = m.FirstName,
                LastName        = m.LastName,
                EnrollDate      = m.EnrollDate,
                AccountStatus   = m.Status.ToString(),
                SponsorMemberId = m.SponsorMemberId,
                DualPath        = dualPath
            };
    }

    private async Task EnrichAsync(List<TeamStatusChangeView> rows, string viewerId, StatusChangesScope scope, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.MemberId).ToList();

        var current = (await _db.MembershipSubscriptions.AsNoTracking()
                .Where(s => ids.Contains(s.MemberId) && !s.IsDeleted)
                .ToListAsync(ct))
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartDate).ThenByDescending(s => s.CreationDate).First());

        // N2: el nivel lo da el producto de la orden de la suscripción actual.
        var levelBySub = await _db.GetMembershipLevelIdsBySubscriptionAsync(current.Values.ToList(), ct);
        var levelNames = await _db.MembershipLevels.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Name, ct);

        // N3: «Calificado» en un solo sitio.
        var qualified = await _qualification.AreQualifiedAsync(ids, ct);

        var accountLast = await _db.MemberStatusHistories.AsNoTracking()
            .Where(h => ids.Contains(h.MemberId))
            .GroupBy(h => h.MemberId)
            .Select(g => new { MemberId = g.Key, Last = g.Max(h => h.ChangedAt) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Last, ct);

        var membershipLast = (await _db.MembershipStatusHistories.AsNoTracking()
                .Where(h => ids.Contains(h.MemberId))
                .GroupBy(h => h.SubscriptionId)
                .Select(g => new { SubscriptionId = g.Key, Last = g.Max(h => h.ChangedAt) })
                .ToListAsync(ct))
            .ToDictionary(x => x.SubscriptionId, x => x.Last);

        var uplines = (await _db.DualTeamTree.AsNoTracking()
                .Where(d => ids.Contains(d.MemberId))
                .Select(d => new { d.MemberId, d.ParentMemberId, d.CreationDate })
                .ToListAsync(ct))
            .GroupBy(d => d.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.CreationDate).First().ParentMemberId);

        var nameIds = rows.Where(r => r.SponsorMemberId != null).Select(r => r.SponsorMemberId!)
            .Concat(uplines.Values.Where(v => v != null).Select(v => v!))
            .Distinct().ToList();
        var names = await _db.MemberProfiles.AsNoTracking()
            .Where(m => nameIds.Contains(m.MemberId))
            .Select(m => new { m.MemberId, FullName = m.FirstName + " " + m.LastName })
            .ToDictionaryAsync(m => m.MemberId, m => m.FullName, ct);

        var legs = scope == StatusChangesScope.DualTeam
            ? (await _db.DualTeamTree.AsNoTracking()
                    .Where(d => d.ParentMemberId == viewerId)
                    .Select(d => new { d.MemberId, d.Side })
                    .ToListAsync(ct))
                .GroupBy(d => d.MemberId)
                .ToDictionary(g => g.Key, g => g.First().Side)
            : new Dictionary<string, TreeSide>();

        foreach (var row in rows)
        {
            current.TryGetValue(row.MemberId, out var sub);

            row.AccountStatusChangedAt = accountLast.TryGetValue(row.MemberId, out var last) ? last : null;
            row.MembershipStatus       = sub?.SubscriptionStatus.ToString() ?? "None";
            row.MembershipLevelName    = sub is not null && levelBySub.TryGetValue(sub.Id, out var levelId)
                ? levelNames.GetValueOrDefault(levelId)
                : null;
            row.IsQualified            = qualified.GetValueOrDefault(row.MemberId);

            row.MembershipStatusChangedAt = sub is null
                ? null
                : membershipLast.TryGetValue(sub.Id, out var changed) ? changed : StatusDate(sub);

            row.SponsorFullName    = names.GetValueOrDefault(row.SponsorMemberId ?? string.Empty);
            row.DualUplineMemberId = uplines.GetValueOrDefault(row.MemberId);
            row.DualUplineFullName = names.GetValueOrDefault(row.DualUplineMemberId ?? string.Empty);

            if (scope == StatusChangesScope.DualTeam && row.DualPath is not null)
            {
                var segments = row.DualPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var at       = Array.IndexOf(segments, viewerId);
                var gateway  = at >= 0 && at + 1 < segments.Length ? segments[at + 1] : null;
                row.Leg      = gateway is not null && legs.TryGetValue(gateway, out var side) ? side.ToString() : null;
            }

            row.DualPath = null; // nunca sale del servidor
        }
    }

    /// <summary>
    /// Sin historial de la suscripción (R4: el historial empieza con el Bloque 5), la fecha de
    /// reserva es la propia del estado: HoldDate para OnHold y HoldByBilling, CancellationDate para
    /// Cancelled, EndDate para Expired y StartDate para el resto.
    /// </summary>
    internal static DateTime? StatusDate(MembershipSubscription s) => s.SubscriptionStatus switch
    {
        MembershipStatus.OnHold or MembershipStatus.HoldByBilling => s.HoldDate ?? s.StartDate,
        MembershipStatus.Cancelled                                => s.CancellationDate ?? s.StartDate,
        MembershipStatus.Expired                                  => s.EndDate ?? s.StartDate,
        _                                                         => s.StartDate
    };

    /// <summary>Solo se ordena y filtra en la base por columnas de la proyección ligera (R6).</summary>
    private static void Sanitize(GridDataRequest request)
    {
        request.Sorts   = request.Sorts.Where(s => DbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => DbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(TeamStatusChangeView.LastName), Direction = "asc" });
    }
}
