using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetAccessAuditGrid;

/// <summary>
/// Grid de servidor del registro de auditoría de acceso (MemberStatusHistories). Antes la pantalla cargaba una página
/// y el filtro y el orden de Syncfusion solo veían esas filas; aquí se aplican en SQL sobre toda la tabla. Los
/// filtros "igual a" de los estados viejo y nuevo se pasan a la entidad antes de proyectar (comparación de entero en
/// vez de un CASE por fila); el resto de columnas son directas, así que su filtro va a la columna tal cual.
/// </summary>
public class GetAccessAuditGridHandler
    : IRequestHandler<GetAccessAuditGridQuery, Result<PagedResult<AccessAuditEntryDto>>>
{
    private readonly AppDbContext _db;

    public GetAccessAuditGridHandler(AppDbContext db) => _db = db;

    /// <summary>Máximo de filas por página; la pantalla ofrece 25, 50 y 100.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Sin cuadro de búsqueda en la pantalla; si llega, por prefijo del miembro, que tiene índice.</summary>
    private static readonly string[] SearchableFields = { nameof(MemberStatusHistory.MemberId) };

    public async Task<Result<PagedResult<AccessAuditEntryDto>>> Handle(
        GetAccessAuditGridQuery request, CancellationToken cancellationToken)
    {
        var grid = request.Grid ?? new GridDataRequest();
        grid.Filters ??= new List<GridFilter>();

        // Más reciente primero, como la lectura antigua: ChangedAt tiene índice propio.
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(AccessAuditEntryDto.ChangedAt), Direction = "desc" } };

        IQueryable<MemberStatusHistory> query = _db.MemberStatusHistories.AsNoTracking();

        // Un valor que no es un estado no casa con nada, igual que no casaría comparando el texto.
        if (grid.TryTakeEqualFilters(nameof(AccessAuditEntryDto.OldStatus), out var viejos))
            foreach (var v in viejos)
                query = EsEstado(v, out var e) ? query.Where(h => h.OldStatus == e) : query.Where(h => false);

        if (grid.TryTakeEqualFilters(nameof(AccessAuditEntryDto.NewStatus), out var nuevos))
            foreach (var v in nuevos)
                query = EsEstado(v, out var e) ? query.Where(h => h.NewStatus == e) : query.Where(h => false);

        if (!string.IsNullOrWhiteSpace(grid.Search))
        {
            query = query.ApplySearch(grid.Search, SearchableFields, porPrefijo: true);
            grid.Search = null;
        }

        var projected = query.Select(h => new AccessAuditEntryDto
        {
            Id        = h.Id,
            MemberId  = h.MemberId,
            OldStatus = h.OldStatus.ToString(),
            NewStatus = h.NewStatus.ToString(),
            Reason    = h.Reason,
            ChangedBy = h.CreatedBy,
            ChangedAt = h.ChangedAt
        });

        var result = await projected.ToGridResultAsync(grid, Array.Empty<string>(), MaxPageSize, cancellationToken);
        return Result<PagedResult<AccessAuditEntryDto>>.Success(result);
    }

    private static bool EsEstado(string valor, out MemberAccountStatus estado) =>
        Enum.TryParse(valor, true, out estado) && Enum.IsDefined(estado);
}
