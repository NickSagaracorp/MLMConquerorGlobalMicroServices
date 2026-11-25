using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Security;
using MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignups;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Security.GetFlaggedSignupsGrid;

/// <summary>
/// Grid de eventos de huella del alta con filtro, orden y paginación en el servidor. Hay un evento por intento de
/// alta, así que la tabla crece con las altas; antes la página traía 25 filas y el filtro de Syncfusion solo veía
/// esas. Aquí filtrar y ordenar abarca todos los eventos que dejan pasar los filtros de la página.
/// </summary>
public class GetFlaggedSignupsGridHandler
    : IRequestHandler<GetFlaggedSignupsGridQuery, Result<PagedResult<FlaggedSignupDto>>>
{
    /// <summary>Tope de filas por página; la página de AdminWeb ofrece 25, 50 y 100.</summary>
    internal const int MaxPageSize = 100;

    private static readonly string[] SearchableFields =
    {
        nameof(FlaggedSignupDto.VisitorId),
        nameof(FlaggedSignupDto.IpAddress),
        nameof(FlaggedSignupDto.SponsorReplicateSite),
    };

    private readonly AppDbContext _db;

    public GetFlaggedSignupsGridHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<FlaggedSignupDto>>> Handle(
        GetFlaggedSignupsGridQuery q, CancellationToken ct)
    {
        var grid = q.Grid ?? new GridDataRequest();
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(FlaggedSignupDto.CreationDate), Direction = "desc" } };
        grid.Filters ??= new List<GridFilter>();

        // Los mismos filtros de página que el listado GET, sobre la tabla y antes de proyectar.
        var query = GetFlaggedSignupsHandler.ApplyPageFilters(
            _db.SignupRiskFingerprints.AsNoTracking(),
            q.VisitorId?.Trim(), q.IpAddress?.Trim(), q.SponsorReplicateSite?.Trim(),
            q.From, q.To, q.OnlyFlagged, q.IncludeCleared);

        var projected = query.Select(x => new FlaggedSignupDto
        {
            Id                   = x.Id,
            VisitorId            = x.VisitorId,
            Flow                 = x.Flow.ToString(),
            SponsorReplicateSite = x.SponsorReplicateSite,
            IpAddress            = x.IpAddress,
            UserAgent            = x.UserAgent,
            OrderId              = x.OrderId,
            MemberId             = x.MemberId,
            IsFlagged            = x.IsFlagged,
            FlagReason           = x.FlagReason,
            Cleared              = x.Cleared,
            ClearedAt            = x.ClearedAt,
            ClearedBy            = x.ClearedBy,
            ClearReason          = x.ClearReason,
            CreationDate         = x.CreationDate
        });

        var result = await projected.ToGridResultAsync(grid, SearchableFields, MaxPageSize, ct);
        return Result<PagedResult<FlaggedSignupDto>>.Success(result);
    }
}
