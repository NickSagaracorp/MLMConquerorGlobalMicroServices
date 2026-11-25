using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TicketAdmin.GetAdminTicketsGrid;

/// <summary>
/// Grid de tickets del admin con filtro, orden y paginación en el servidor. Antes la página traía una sola página de
/// la API y el filtro de Syncfusion solo veía esas filas: filtrar por estado o por miembro daba resultados
/// incompletos. Aquí todo va en una sola consulta proyectada al DTO, así que filtrar y ordenar abarca la tabla entera.
/// </summary>
public class GetAdminTicketsGridHandler
    : IRequestHandler<GetAdminTicketsGridQuery, Result<PagedResult<AdminTicketDto>>>
{
    /// <summary>Tope de filas por página; la página de AdminWeb ofrece 25, 50 y 100.</summary>
    internal const int MaxPageSize = 100;

    /// <summary>Columnas de texto en las que busca la búsqueda libre, si algún día la página la usa.</summary>
    private static readonly string[] SearchableFields =
    {
        nameof(AdminTicketDto.TicketNumber),
        nameof(AdminTicketDto.Subject),
        nameof(AdminTicketDto.MemberId),
    };

    private readonly AppDbContext _db;

    public GetAdminTicketsGridHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminTicketDto>>> Handle(
        GetAdminTicketsGridQuery request, CancellationToken cancellationToken)
    {
        var grid = request.Grid ?? new GridDataRequest();

        // Sin orden explícito, los más nuevos primero: es lo que hacía el listado y un orden fijo hace estable la
        // paginación.
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(AdminTicketDto.CreationDate), Direction = "desc" } };
        grid.Filters ??= new List<GridFilter>();

        var query = _db.Tickets.AsNoTracking();

        // El estado del desplegable se aplica sobre la tabla, antes de proyectar, para que use la columna tal cual
        // (y su índice) en vez del texto del enum. Un valor que no es un estado se ignora, como en Members.
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<TicketStatus>(request.Status, ignoreCase: true, out var status)
            && Enum.IsDefined(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var projected = query.Select(t => new AdminTicketDto
        {
            Id               = t.Id,
            TicketNumber     = t.TicketNumber,
            Subject          = t.Subject,
            // El cuerpo no se pinta en el grid y puede ser largo: se deja vacío para no traer 100 cuerpos por
            // página. El detalle del ticket sigue entregándolo completo.
            Body             = string.Empty,
            MemberId         = t.MemberId,
            Status           = t.Status.ToString(),
            Priority         = t.Priority.ToString(),
            CategoryName     = t.Category != null ? t.Category.Name : null,
            AssignedToUserId = t.AssignedToUserId,
            EscalationLevel  = (int)t.EscalationLevel,
            CreationDate     = t.CreationDate,
            CommentCount     = t.Comments.Count
        });

        var result = await projected.ToGridResultAsync(grid, SearchableFields, MaxPageSize, cancellationToken);
        return Result<PagedResult<AdminTicketDto>>.Success(result);
    }
}
