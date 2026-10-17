using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Events;

/// <inheritdoc />
/// <remarks>
/// El equipo es el subárbol de genealogía del que mira, ÉL INCLUIDO (J20): un miembro pertenece si
/// su nodo MÁS RECIENTE cuelga del prefijo (R8, con TreePrefix). Las entradas y la primera fecha son
/// subconsultas correlacionadas sobre (EventId, MemberId), que el índice de esa pareja resuelve con
/// un seek por fila de la página. Todavía no hay importación (N5): hasta entonces sale vacío.
/// </remarks>
public sealed class TeamEventRegistrationsService : ITeamEventRegistrationsService
{
    public const int MaxEvents = 50;

    private static readonly string[] DbFields =
    {
        nameof(TeamEventRegistrationView.MemberId),
        nameof(TeamEventRegistrationView.FirstName),
        nameof(TeamEventRegistrationView.LastName),
        nameof(TeamEventRegistrationView.EnrollDate),
        nameof(TeamEventRegistrationView.Registered),
        nameof(TeamEventRegistrationView.RegisteredAt),
        nameof(TeamEventRegistrationView.Tickets)
    };

    private static readonly string[] SearchableFields =
    {
        nameof(TeamEventRegistrationView.MemberId),
        nameof(TeamEventRegistrationView.FirstName),
        nameof(TeamEventRegistrationView.LastName)
    };

    private readonly AppDbContext          _db;
    private readonly IQualificationService _qualification;

    public TeamEventRegistrationsService(AppDbContext db, IQualificationService qualification)
    {
        _db            = db;
        _qualification = qualification;
    }

    public Task<List<TeamEventView>> GetActiveEventsAsync(CancellationToken ct = default) =>
        _db.CorporateEvents.AsNoTracking()
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .Take(MaxEvents)
            .Select(e => new TeamEventView { Id = e.Id, Title = e.Title, EventDate = e.EventDate, Location = e.Location })
            .ToListAsync(ct);

    public async Task<Result<PagedResult<TeamEventRegistrationView>>> GetGridAsync(
        string memberId, string eventId, bool registeredOnly, GridDataRequest request, CancellationToken ct = default)
    {
        if (!await IsVisibleAsync(eventId, ct))
            return Result<PagedResult<TeamEventRegistrationView>>.Failure(TeamEventErrors.EventNotFound, "Event not found.");

        var prefix = await PrefixAsync(memberId, ct);
        if (prefix is null)
            return Result<PagedResult<TeamEventRegistrationView>>.Success(new PagedResult<TeamEventRegistrationView>
            {
                Page = Math.Max(1, request.Page), PageSize = request.PageSize <= 0 ? 20 : request.PageSize
            });

        Sanitize(request);

        var regs = _db.CorporateEventRegistrations.AsNoTracking().Where(r => r.EventId == eventId);

        var query =
            from m in Team(prefix)
            where !registeredOnly || regs.Any(r => r.MemberId == m.MemberId)
            select new TeamEventRegistrationView
            {
                MemberId     = m.MemberId,
                FirstName    = m.FirstName,
                LastName     = m.LastName,
                EnrollDate   = m.EnrollDate,
                Registered   = regs.Any(r => r.MemberId == m.MemberId),
                RegisteredAt = regs.Where(r => r.MemberId == m.MemberId).Min(r => (DateTime?)r.RegisteredAt),
                Tickets      = regs.Where(r => r.MemberId == m.MemberId).Sum(r => (int?)r.Tickets) ?? 0
            };

        var page = await query.ToGridResultAsync(request, SearchableFields, ct: ct);
        var rows = page.Items as List<TeamEventRegistrationView> ?? page.Items.ToList();
        await EnrichAsync(rows, ct);
        page.Items = rows;
        return Result<PagedResult<TeamEventRegistrationView>>.Success(page);
    }

    public async Task<Result<TeamEventTotalsView>> GetTotalsAsync(string memberId, string eventId, CancellationToken ct = default)
    {
        if (!await IsVisibleAsync(eventId, ct))
            return Result<TeamEventTotalsView>.Failure(TeamEventErrors.EventNotFound, "Event not found.");

        var prefix = await PrefixAsync(memberId, ct);
        if (prefix is null) return Result<TeamEventTotalsView>.Success(new TeamEventTotalsView());

        var teamIds = Team(prefix).Select(m => m.MemberId);
        var inTeam  = _db.CorporateEventRegistrations.AsNoTracking()
            .Where(r => r.EventId == eventId && teamIds.Contains(r.MemberId));

        return Result<TeamEventTotalsView>.Success(new TeamEventTotalsView
        {
            Registered = await inTeam.Select(r => r.MemberId).Distinct().CountAsync(ct),
            Tickets    = await inTeam.SumAsync(r => (int?)r.Tickets, ct) ?? 0
        });
    }

    private Task<bool> IsVisibleAsync(string eventId, CancellationToken ct) =>
        _db.CorporateEvents.AsNoTracking().AnyAsync(e => e.Id == eventId && e.IsActive && !e.IsDeleted, ct);

    /// <summary>Prefijo del nodo más reciente del que mira (R8), con barra final; null si no tiene nodo.</summary>
    private async Task<string?> PrefixAsync(string memberId, CancellationToken ct)
    {
        var path = await _db.GenealogyTree.AsNoTracking()
            .Where(g => g.MemberId == memberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.HierarchyPath)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(path) ? null : TreePrefix.Of(path);
    }

    /// <summary>Los perfiles del equipo, él incluido: su nodo más reciente cuelga del prefijo (R8).</summary>
    private IQueryable<Domain.Entities.Member.MemberProfile> Team(string prefix) =>
        from m in _db.MemberProfiles.AsNoTracking()
        let node = _db.GenealogyTree
            .Where(g => g.MemberId == m.MemberId)
            .OrderByDescending(g => g.CreationDate)
            .Select(g => g.HierarchyPath)
            .FirstOrDefault()
        where node != null && node.StartsWith(prefix)
        select m;

    private async Task EnrichAsync(List<TeamEventRegistrationView> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        var ids = rows.Select(r => r.MemberId).ToList();

        var qualified = await _qualification.AreQualifiedAsync(ids, ct);

        var ranks = (await _db.MemberRankHistories.AsNoTracking()
                .Include(r => r.RankDefinition)
                .Where(r => ids.Contains(r.MemberId))
                .ToListAsync(ct))
            .GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.AchievedAt).First().RankDefinition?.Name);

        foreach (var row in rows)
        {
            row.IsQualified = qualified.GetValueOrDefault(row.MemberId);
            row.RankName    = ranks.GetValueOrDefault(row.MemberId);
        }
    }

    private static void Sanitize(GridDataRequest request)
    {
        request.Sorts   = request.Sorts.Where(s => DbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => DbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.LastName), Direction = "asc" });
    }
}
