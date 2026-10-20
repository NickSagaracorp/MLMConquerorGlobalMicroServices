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
/// Plan Q: una sola consulta agrupada y paginada en SQL. Las altas del rango (3 meses hacia atrás como mucho, N4) cuyo
/// patrocinador cuelga del Node del que mira (él incluido; un nodo vivo por miembro, DQ9) se agrupan por patrocinador,
/// con el tipo y el nivel de alta guardado de cada inscrito (MemberProfiles.SignupMembershipLevelId, regla N2 del Plan J,
/// <see cref="SignupMembershipLevel"/>). Búsqueda, orden y página también en SQL.
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
            .ThenByDescending(g => g.Id)      // nodo vigente con desempate por Id (revisión M-e)
            .Select(g => new { g.Node, g.Level })
            .FirstOrDefaultAsync(ct);
        if (me?.Node is null) return Result<PagedResult<EnrollmentActivityRowView>>.Success(Empty(request));

        var start  = range.Start;
        var end    = range.End;
        var root   = me.Node;

        // Plan Q: las altas del rango cuyo patrocinador cuelga del que mira, agrupadas por patrocinador EN SQL. El nodo del
        // patrocinador es unico (UX_GenealogyTree_MemberId_Vivo); el nivel de alta viene guardado (SignupMembershipLevelId,
        // regla N2). Antes: TOP 1 correlacionado con IsDescendantOf por alta, los pedidos de los inscritos por lista de ids
        // y el pivote en memoria (mas de 30 s con 1.000.000).
        var porPatrocinador =
            from m in _db.MemberProfiles.AsNoTracking()
            where m.EnrollDate >= start && m.EnrollDate < end && m.SponsorMemberId != null
            join g in _db.GenealogyTree.AsNoTracking().Under(root) on m.SponsorMemberId equals g.MemberId
            group new { m.MemberType, m.SignupMembershipLevelId } by new { g.MemberId, g.Level } into grp
            select new
            {
                grp.Key.MemberId,
                grp.Key.Level,
                Customers   = grp.Count(x => x.MemberType == MemberType.ExternalMember),
                Ambassadors = grp.Count(x => x.MemberType != MemberType.ExternalMember),
                Level1      = grp.Count(x => x.SignupMembershipLevelId == 1),
                Level2      = grp.Count(x => x.SignupMembershipLevelId == 2),
                Level3      = grp.Count(x => x.SignupMembershipLevelId == 3),
                Level4      = grp.Count(x => x.SignupMembershipLevelId == 4),
                OtherLevel  = grp.Count(x => x.SignupMembershipLevelId == null
                                          || (x.SignupMembershipLevelId != 1 && x.SignupMembershipLevelId != 2
                                              && x.SignupMembershipLevelId != 3 && x.SignupMembershipLevelId != 4))
            };

        var query =
            from a in porPatrocinador
            join p in _db.MemberProfiles.AsNoTracking() on a.MemberId equals p.MemberId
            select new EnrollmentActivityRowView
            {
                MemberId             = a.MemberId,
                FullName             = p.FirstName + " " + p.LastName,
                Level                = a.Level - me.Level,
                Customers            = a.Customers,
                Ambassadors          = a.Ambassadors,
                Level1               = a.Level1,
                Level2               = a.Level2,
                Level3               = a.Level3,
                Level4               = a.Level4,
                OtherLevel           = a.OtherLevel,
                EnrollmentTeamPoints = _db.MemberStatistics.Where(s => s.MemberId == a.MemberId)
                                          .OrderBy(s => s.Id).Select(s => s.EnrollmentPoints).FirstOrDefault()
            };

        if (request.Sorts.Count == 0)
            request.Sorts.Add(new GridSort { Field = nameof(EnrollmentActivityRowView.Ambassadors), Direction = "desc" });

        return Result<PagedResult<EnrollmentActivityRowView>>.Success(
            await query.ToGridResultAsync(request, SearchableFields, ct: ct));
    }

    private static PagedResult<EnrollmentActivityRowView> Empty(GridDataRequest request) => new()
    {
        Page     = Math.Max(1, request.Page),
        PageSize = request.PageSize <= 0 ? 20 : request.PageSize
    };
}
