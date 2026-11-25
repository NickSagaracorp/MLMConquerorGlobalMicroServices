using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Subscriptions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Subscriptions.GetSubscriptionsGrid;

/// <summary>
/// Grid de Subscriptions del admin. Antes la pagina pedia UNA pagina al GET y el filtro y el orden de Syncfusion solo
/// trabajaban sobre esas filas, asi que con un millon de suscripciones daban resultados equivocados. Aqui todo va en
/// una sola consulta: estado de la pagina sobre la tabla, proyeccion al DTO dentro del IQueryable y despues filtros,
/// orden y pagina con <see cref="GridQueryableExtensions.ToGridResultAsync{T}"/>, sobre TODAS las filas.
/// </summary>
public class GetSubscriptionsGridHandler
    : IRequestHandler<GetSubscriptionsGridQuery, Result<PagedResult<SubscriptionDto>>>
{
    /// <summary>Tope de filas por pagina; la pagina ofrece 25, 50 y 100.</summary>
    public const int MaxPageSize = 100;

    public const string InvalidStatusCode = "INVALID_STATUS";

    /// <summary>
    /// La busqueda libre solo mira el ID del miembro y por prefijo: es la columna con indice. "Contiene" en el nivel o
    /// el motivo recorreria la tabla entera; para eso estan los filtros de su columna.
    /// </summary>
    private static readonly string[] SearchableFields = { nameof(MembershipSubscription.MemberId) };

    private readonly AppDbContext _db;

    public GetSubscriptionsGridHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<SubscriptionDto>>> Handle(
        GetSubscriptionsGridQuery request, CancellationToken cancellationToken)
    {
        var grid = request.Grid;

        // Sin orden pedido, las mas nuevas primero: es lo que el admin espera ver y lo que mostraba el GET.
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(SubscriptionDto.CreationDate), Direction = "desc" } };

        var query = _db.MembershipSubscriptions.AsNoTracking();

        // El estado del desplegable se aplica sobre la columna del enum, antes de proyectar, para que use su indice;
        // filtrar por el texto proyectado obligaria a SQL Server a evaluar un CASE fila por fila.
        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            if (!Enum.TryParse<MembershipStatus>(request.StatusFilter, true, out var estado) ||
                int.TryParse(request.StatusFilter, out _) || !Enum.IsDefined(estado))
            {
                return Result<PagedResult<SubscriptionDto>>.Failure(InvalidStatusCode,
                    $"Status '{request.StatusFilter}' is not valid.");
            }
            query = query.Where(s => s.SubscriptionStatus == estado);
        }

        // La busqueda tambien sobre la tabla y antes de proyectar, por el mismo motivo que en el listado de miembros.
        if (!string.IsNullOrWhiteSpace(grid.Search))
        {
            query = query.ApplySearch(grid.Search, SearchableFields, porPrefijo: true);
            grid.Search = null;
        }

        // El nombre del nivel sale de la navegacion (un JOIN), no de una subconsulta por fila.
        var projected = query.Select(s => new SubscriptionDto
        {
            Id                  = s.Id,
            MemberId            = s.MemberId,
            MembershipLevelId   = s.MembershipLevelId,
            MembershipLevelName = s.MembershipLevel != null ? s.MembershipLevel.Name : string.Empty,
            Status              = s.SubscriptionStatus.ToString(),
            ChangeReason        = s.ChangeReason.ToString(),
            StartDate           = s.StartDate,
            ExpirationDate      = s.EndDate,
            IsAutoRenew         = s.IsAutoRenew,
            IsFree              = s.IsFree,
            CreationDate        = s.CreationDate
        });

        var result = await projected.ToGridResultAsync(grid, Array.Empty<string>(), MaxPageSize, cancellationToken);
        return Result<PagedResult<SubscriptionDto>>.Success(result);
    }
}
