using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetAdminCommissionsGrid;

/// <summary>
/// Grid de servidor de comisiones del admin (~2,4 millones de filas en la base de escala). Antes la pantalla cargaba
/// una página por GET y el filtro y el orden de Syncfusion solo actuaban sobre esas filas, así que daban resultados
/// equivocados. Aquí filtro, orden y paginación se aplican en SQL sobre toda la tabla: el estado del desplegable y
/// los filtros "igual a" de la columna Status van contra la entidad antes de proyectar (comparación de entero, no un
/// CASE por fila), y el resto de columnas se filtra sobre la proyección, que es un solo SELECT con el LEFT JOIN al
/// tipo de comisión. Sin caché: la pantalla marca pagadas y cancela, y una página cacheada mostraría el estado viejo.
/// </summary>
public class GetAdminCommissionsGridHandler
    : IRequestHandler<GetAdminCommissionsGridQuery, Result<PagedResult<AdminCommissionDto>>>
{
    private readonly AppDbContext _db;

    public GetAdminCommissionsGridHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Máximo de filas por página; la pantalla ofrece 25, 50 y 100. Antes ofrecía hasta 1000, que con la tabla
    /// entera detrás son lecturas y pintados que no aportan nada.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// La pantalla no tiene cuadro de búsqueda; si un día lo tiene, busca por prefijo en el beneficiario, que tiene
    /// índice (BeneficiaryMemberId, Status).
    /// </summary>
    private static readonly string[] SearchableFields = { nameof(CommissionEarning.BeneficiaryMemberId) };

    public async Task<Result<PagedResult<AdminCommissionDto>>> Handle(
        GetAdminCommissionsGridQuery request, CancellationToken cancellationToken)
    {
        var grid = request.Grid ?? new GridDataRequest();
        grid.Filters ??= new List<GridFilter>();

        // Orden por defecto: lo más reciente primero, como la lectura antigua. EarnedDate encabeza el índice
        // IX_CommissionEarnings_EarnedDate_Beneficiary, así que la primera página sale del índice sin ordenar la tabla.
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(AdminCommissionDto.EarnedDate), Direction = "desc" } };

        IQueryable<CommissionEarning> query = _db.CommissionEarnings.AsNoTracking();

        // Desplegable de estado de la página. Un valor que no es un estado no filtra (el GET antiguo lo ignoraba).
        if (EsEstado(request.Status, out var estado))
            query = query.Where(c => c.Status == estado);

        // Filtro "igual a" de la columna Status: se pasa a la entidad. Un valor que no es un estado no casa con nada,
        // igual que no casaría comparando el texto.
        if (grid.TryTakeEqualFilters(nameof(AdminCommissionDto.Status), out var estadosDeColumna))
            foreach (var valor in estadosDeColumna)
                query = EsEstado(valor, out var e) ? query.Where(c => c.Status == e) : query.Where(c => false);

        // La búsqueda libre (si llega) se aplica sobre la tabla, por prefijo y en columnas con índice.
        if (!string.IsNullOrWhiteSpace(grid.Search))
        {
            query = query.ApplySearch(grid.Search, SearchableFields, porPrefijo: true);
            grid.Search = null;
        }

        // Una sola proyección: el nombre del tipo sale de la navegación (LEFT JOIN), no de una subconsulta por fila.
        var projected = query.Select(c => new AdminCommissionDto
        {
            Id                  = c.Id,
            BeneficiaryMemberId = c.BeneficiaryMemberId,
            CommissionTypeName  = c.CommissionType != null ? c.CommissionType.Name : string.Empty,
            Amount              = c.Amount,
            Status              = c.Status.ToString(),
            EarnedDate          = c.EarnedDate,
            PaymentDate         = c.PaymentDate,
            IsManualEntry       = c.IsManualEntry
        });

        var result = await projected.ToGridResultAsync(grid, Array.Empty<string>(), MaxPageSize, cancellationToken);
        return Result<PagedResult<AdminCommissionDto>>.Success(result);
    }

    private static bool EsEstado(string? valor, out CommissionEarningStatus estado)
    {
        estado = default;
        return !string.IsNullOrWhiteSpace(valor) && Enum.TryParse(valor, true, out estado) && Enum.IsDefined(estado);
    }
}
