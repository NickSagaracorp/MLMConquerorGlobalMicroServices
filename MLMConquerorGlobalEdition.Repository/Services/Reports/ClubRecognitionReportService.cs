using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

public interface IClubRecognitionReportService
{
    /// <summary>Una página. La búsqueda llega en <see cref="GridDataRequest.Search"/> (la del filtro se ignora).</summary>
    Task<PagedResult<ClubRecognitionRowView>> GetPageAsync(ClubRecognitionFilter filter, GridDataRequest request,
        CancellationToken ct = default);

    /// <summary>Todas las filas que cumplen el filtro (exportación).</summary>
    Task<IReadOnlyList<ClubRecognitionRowView>> GetAllAsync(ClubRecognitionFilter filter, CancellationToken ct = default);

    /// <summary>Los registros de un miembro por orden de nivel (pestaña Recognition), aunque sea cuenta corporativa.</summary>
    Task<IReadOnlyList<ClubRecognitionRowView>> GetForMemberAsync(string memberId, CancellationToken ct = default);
}

/// <summary>
/// Lectura de Club Recognition. Vista y nivel se filtran en SQL; búsqueda (nombre, email, país, sin mayúsculas), orden y
/// paginación, en memoria: la tabla tiene una fila por Ambassador y nivel alcanzado. Sin caché: lo que el admin captura se
/// ve al momento. Las vistas no muestran cuentas corporativas. Orden: Reached de la más reciente a la más antigua, nivel
/// de mayor a menor, nombre.
/// </summary>
public class ClubRecognitionReportService : IClubRecognitionReportService
{
    private readonly AppDbContext _db;

    public ClubRecognitionReportService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ClubRecognitionRowView>> GetPageAsync(ClubRecognitionFilter filter,
        GridDataRequest request, CancellationToken ct = default)
    {
        var rows = await GetAllAsync(filter with { Search = request.Search }, ct);
        var (page, pageSize) = GridQueryableExtensions.ResolvePage(request);
        return new PagedResult<ClubRecognitionRowView>
        {
            Items      = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<IReadOnlyList<ClubRecognitionRowView>> GetAllAsync(ClubRecognitionFilter filter,
        CancellationToken ct = default)
    {
        var term = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var rows = await LoadAsync(Filtered(filter), includeCorporate: false, ct);
        return rows
            .Where(r => term is null
                     || r.MemberName.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || r.Email.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || r.Country.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ReachedDate)
            .ThenByDescending(r => r.LevelSortOrder)
            .ThenBy(r => r.MemberName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<ClubRecognitionRowView>> GetForMemberAsync(string memberId,
        CancellationToken ct = default) =>
        (await LoadAsync(_db.MemberRecognitions.AsNoTracking().Where(r => r.MemberId == memberId), includeCorporate: true, ct))
            .OrderBy(r => r.LevelSortOrder).ThenBy(r => r.Id)
            .ToList();

    private IQueryable<MemberRecognition> Filtered(ClubRecognitionFilter f)
    {
        var q = _db.MemberRecognitions.AsNoTracking();
        if (f.LevelId is not null) q = q.Where(r => r.RecognitionLevelId == f.LevelId);
        return f.View switch
        {
            ClubRecognitionView.NotRecognizedOnline => q.Where(r => r.CeoCallMonth == null && r.CeoCallOptionId == null),
            ClubRecognitionView.RecognizedOnline    => q.Where(r => r.CeoCallMonth != null || r.CeoCallOptionId != null),
            ClubRecognitionView.NotRecognizedEvents => q.Where(r => r.EventId == null && r.EventOptionId == null),
            ClubRecognitionView.RecognizedEvents    => q.Where(r => r.EventId != null || r.EventOptionId != null),
            _                                       => q
        };
    }

    private async Task<List<ClubRecognitionRowView>> LoadAsync(IQueryable<MemberRecognition> records,
        bool includeCorporate, CancellationToken ct)
    {
        var rows = await (
            from r in records
            join m in _db.MemberProfiles.AsNoTracking() on r.MemberId equals m.MemberId
            where includeCorporate || !m.IsCorporateAccount
            select new
            {
                r.Id, r.MemberId, m.FirstName, m.LastName, m.Email, m.ReplicateSiteSlug, m.Country,
                r.RecognitionLevelId,
                LevelName       = r.Level!.Name,
                LevelSortOrder  = r.Level!.SortOrder,
                Reward          = r.Level!.Reward,
                RewardNeedsSize = r.Level!.RewardNeedsSize,
                r.ReachedDate, r.QualifyingTotal, r.Source,
                r.CeoCallMonth, r.CeoCallOptionId, r.CeoCallReason,
                CeoCallOptionName = r.CeoCallOption == null ? null : r.CeoCallOption.Name,
                r.EventId, r.EventOptionId, r.EventReason,
                EventTitle      = r.Event == null ? null : r.Event.Title,
                EventOptionName = r.EventOption == null ? null : r.EventOption.Name,
                r.RewardSize, r.Notes
            }).ToListAsync(ct);

        return rows.Select(x => new ClubRecognitionRowView
        {
            Id                 = x.Id,
            MemberId           = x.MemberId,
            MemberName         = $"{x.FirstName} {x.LastName}".Trim(),
            Email              = x.Email,
            Username           = x.ReplicateSiteSlug,
            Country            = x.Country,
            RecognitionLevelId = x.RecognitionLevelId,
            LevelName          = x.LevelName,
            LevelSortOrder     = x.LevelSortOrder,
            Reward             = x.Reward,
            RewardNeedsSize    = x.RewardNeedsSize,
            ReachedDate        = x.ReachedDate,
            QualifyingTotal    = x.QualifyingTotal,
            Source             = x.Source,
            CeoCallMonth       = x.CeoCallMonth,
            CeoCallOptionId    = x.CeoCallOptionId,
            CeoCallReason      = x.CeoCallReason,
            CeoCallRecognized  = RecognitionText.CeoCall(x.CeoCallMonth, x.CeoCallOptionName, x.CeoCallReason),
            EventId            = x.EventId,
            EventOptionId      = x.EventOptionId,
            EventReason        = x.EventReason,
            EventRecognized    = RecognitionText.Event(x.EventTitle, x.EventOptionName, x.EventReason),
            RewardSize         = x.RewardSize,
            Notes              = x.Notes
        }).ToList();
    }
}
