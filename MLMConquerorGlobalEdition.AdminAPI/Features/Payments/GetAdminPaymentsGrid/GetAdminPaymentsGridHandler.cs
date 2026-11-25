using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPaymentsGrid;

/// <summary>
/// Grid de Payment History del admin. Antes la pagina pedia UNA pagina al GET y el filtro y el orden de Syncfusion solo
/// trabajaban sobre esas filas; ademas el rango de fechas de la cabecera no llegaba al servidor. Aqui los filtros de la
/// pagina van sobre la tabla, la proyeccion al DTO va dentro del IQueryable y despues filtros de columna, orden y
/// pagina con <see cref="GridQueryableExtensions.ToGridResultAsync{T}"/>, sobre TODOS los pagos.
/// </summary>
public class GetAdminPaymentsGridHandler
    : IRequestHandler<GetAdminPaymentsGridQuery, Result<PagedResult<AdminPaymentDto>>>
{
    /// <summary>Tope de filas por pagina; la pagina ofrece 25, 50 y 100.</summary>
    public const int MaxPageSize = 100;

    public const string InvalidStatusCode = "INVALID_STATUS";

    /// <summary>
    /// La busqueda libre solo mira columnas cortas y por prefijo (ID del miembro, ID del pedido y de la transaccion),
    /// las que pueden tener indice. "Contiene" en la razon del fallo recorreria la tabla entera.
    /// </summary>
    private static readonly string[] SearchableFields =
    {
        nameof(PaymentHistory.MemberId),
        nameof(PaymentHistory.OrderId),
        nameof(PaymentHistory.GatewayTransactionId),
    };

    private readonly AppDbContext _db;

    public GetAdminPaymentsGridHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminPaymentDto>>> Handle(
        GetAdminPaymentsGridQuery request, CancellationToken cancellationToken)
    {
        var grid = request.Grid;

        // Sin orden pedido, los mas nuevos primero, como el GET.
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(AdminPaymentDto.CreationDate), Direction = "desc" } };

        var query = _db.PaymentHistories.AsNoTracking();

        // El estado se compara con la columna del enum (el GET la pasaba a texto, lo que impide usar un indice).
        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            if (!Enum.TryParse<PaymentHistoryTransactionStatus>(request.StatusFilter, true, out var estado) ||
                int.TryParse(request.StatusFilter, out _) || !Enum.IsDefined(estado))
            {
                return Result<PagedResult<AdminPaymentDto>>.Failure(InvalidStatusCode,
                    $"Status '{request.StatusFilter}' is not valid.");
            }
            query = query.Where(p => p.TransactionStatus == estado);
        }

        if (!string.IsNullOrWhiteSpace(request.MemberId))
            query = query.Where(p => p.MemberId == request.MemberId);

        // Rango de la cabecera sobre la fecha de creacion: desde el inicio de From hasta el final del dia To.
        if (request.From is { } desde)
        {
            var inicio = desde.Date;
            query = query.Where(p => p.CreationDate >= inicio);
        }
        if (request.To is { } hasta)
        {
            var fin = hasta.Date.AddDays(1);
            query = query.Where(p => p.CreationDate < fin);
        }

        if (!string.IsNullOrWhiteSpace(grid.Search))
        {
            query = query.ApplySearch(grid.Search, SearchableFields, porPrefijo: true);
            grid.Search = null;
        }

        var projected = query.Select(p => new AdminPaymentDto
        {
            Id                   = p.Id,
            MemberId             = p.MemberId,
            OrderId              = p.OrderId,
            Amount               = p.Amount,
            GatewayName          = p.GatewayName,
            GatewayTransactionId = p.GatewayTransactionId,
            Status               = p.TransactionStatus.ToString(),
            FailureReason        = p.FailureReason,
            ProcessedAt          = p.ProcessedAt,
            CreationDate         = p.CreationDate
        });

        var result = await projected.ToGridResultAsync(grid, Array.Empty<string>(), MaxPageSize, cancellationToken);
        return Result<PagedResult<AdminPaymentDto>>.Success(result);
    }
}
