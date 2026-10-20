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
/// su único nodo vivo (Plan Q, DQ9) cuelga del Node del que mira. Las entradas y la primera fecha son
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

        var root = await RootAsync(memberId, ct);
        if (root is null)
            return Result<PagedResult<TeamEventRegistrationView>>.Success(new PagedResult<TeamEventRegistrationView>
            {
                Page = Math.Max(1, request.Page), PageSize = request.PageSize <= 0 ? 20 : request.PageSize
            });

        // Plan Q (DQ7): «Todo el equipo» con mas de TeamGridLimits.FreeMaxRows ordena por estructura (Node) y lo avisa. El
        // tamano lo guarda MemberStatistics.EnrollmentTeamSize del que mira.
        var limitado = !registeredOnly && TeamGridLimits.Limit(request, await TeamSizeAsync(memberId, ct));
        Sanitize(request, defaultSort: !limitado);

        var regs = _db.CorporateEventRegistrations.AsNoTracking().Where(r => r.EventId == eventId);

        // «Solo inscritos» parte de las inscripciones del EVENTO (pocas, por el índice (EventId, MemberId)), agrupadas
        // por miembro, y solo sobre esas comprueba la pertenencia al equipo, ahora uniendo con el rango (un nodo vivo por
        // miembro, Tarea 21 del Plan Q), sin el TOP 1 correlacionado con IsDescendantOf. «Todo el equipo» sí tiene que
        // recorrer el equipo (R16: medido en SqlServerEventosTests).
        var query = registeredOnly
            ? from g in regs.GroupBy(r => r.MemberId)
                  .Select(g => new { MemberId = g.Key, First = g.Min(r => r.RegisteredAt), Tickets = g.Sum(r => r.Tickets) })
              join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
              join n in _db.GenealogyTree.AsNoTracking().Under(root) on m.MemberId equals n.MemberId
              select new TeamEventRegistrationView
              {
                  MemberId     = m.MemberId,
                  FirstName    = m.FirstName,
                  LastName     = m.LastName,
                  EnrollDate   = m.EnrollDate,
                  Registered   = true,
                  RegisteredAt = g.First,
                  Tickets      = g.Tickets
              }
            : from m in Team(root, byStructure: limitado)
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
        page.Items   = rows;
        page.Limited = limitado;
        return Result<PagedResult<TeamEventRegistrationView>>.Success(page);
    }

    public async Task<Result<TeamEventTotalsView>> GetTotalsAsync(string memberId, string eventId, CancellationToken ct = default)
    {
        if (!await IsVisibleAsync(eventId, ct))
            return Result<TeamEventTotalsView>.Failure(TeamEventErrors.EventNotFound, "Event not found.");

        var root = await RootAsync(memberId, ct);
        if (root is null) return Result<TeamEventTotalsView>.Success(new TeamEventTotalsView());

        // Primero las inscripciones del evento y después, solo para ellas, la pertenencia al equipo por el rango.
        var inTeam =
            from r in _db.CorporateEventRegistrations.AsNoTracking()
            where r.EventId == eventId
            join n in _db.GenealogyTree.AsNoTracking().Under(root) on r.MemberId equals n.MemberId
            select r;

        return Result<TeamEventTotalsView>.Success(new TeamEventTotalsView
        {
            Registered = await inTeam.Select(r => r.MemberId).Distinct().CountAsync(ct),
            Tickets    = await inTeam.SumAsync(r => (int?)r.Tickets, ct) ?? 0
        });
    }

    private Task<bool> IsVisibleAsync(string eventId, CancellationToken ct) =>
        _db.CorporateEvents.AsNoTracking().AnyAsync(e => e.Id == eventId && e.IsActive && !e.IsDeleted, ct);

    /// <summary>Node vigente del que mira (R8); null si no tiene nodo.</summary>
    private Task<HierarchyId?> RootAsync(string memberId, CancellationToken ct) =>
        _db.GenealogyTree.AsNoTracking().NodeOfAsync(memberId, ct);

    /// <summary>
    /// Los perfiles del equipo, él incluido (un nodo vivo por miembro: el rango es el equipo vigente). Con
    /// <paramref name="byStructure"/>, en el orden del árbol (Node), antes del join, como Mi equipo (Plan Q, DQ7).
    /// </summary>
    private IQueryable<Domain.Entities.Member.MemberProfile> Team(HierarchyId root, bool byStructure = false)
    {
        var equipo = _db.GenealogyTree.AsNoTracking().CurrentUnder(root);
        if (byStructure) equipo = equipo.OrderBy(g => g.Node);
        return from g in equipo
               join m in _db.MemberProfiles.AsNoTracking() on g.MemberId equals m.MemberId
               select m;
    }

    /// <summary>Plan Q (DQ7): el tamaño del equipo de inscripción del que mira (diferido; basta para un límite).</summary>
    private Task<int> TeamSizeAsync(string memberId, CancellationToken ct) =>
        _db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == memberId)
            .OrderBy(s => s.Id).Select(s => s.EnrollmentTeamSize).FirstOrDefaultAsync(ct);

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

    private static void Sanitize(GridDataRequest request, bool defaultSort = true)
    {
        request.Sorts   = request.Sorts.Where(s => DbFields.Contains(s.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        request.Filters = request.Filters.Where(f => DbFields.Contains(f.Field, StringComparer.OrdinalIgnoreCase)).ToList();
        if (defaultSort && request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(TeamEventRegistrationView.LastName), Direction = "asc" });
    }
}
