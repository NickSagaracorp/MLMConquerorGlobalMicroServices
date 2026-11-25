using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetTokenCodesGrid;

/// <summary>
/// Grid de códigos de token de un miembro con filtro, orden y paginación en el servidor. La página pedía hasta 500
/// códigos de una vez y filtraba y ordenaba solo esos: a un miembro con más códigos le faltaban filas, y el contador
/// de disponibles salía de lo cargado. Aquí todo se resuelve en una consulta sobre todos los códigos del miembro.
/// </summary>
public class GetTokenCodesGridHandler
    : IRequestHandler<GetTokenCodesGridQuery, Result<PagedResult<TokenCodeDto>>>
{
    /// <summary>Tope de filas por página; la página de AdminWeb ofrece 25, 50 y 100.</summary>
    internal const int MaxPageSize = 100;

    private static readonly string[] SearchableFields =
    {
        nameof(TokenCodeGridRow.TokenCode),
        nameof(TokenCodeGridRow.UsedByMemberId),
    };

    private readonly AppDbContext _db;

    public GetTokenCodesGridHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<TokenCodeDto>>> Handle(
        GetTokenCodesGridQuery request, CancellationToken ct)
    {
        var grid = request.Grid ?? new GridDataRequest();
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(TokenCodeGridRow.CreatedDate), Direction = "desc" } };
        grid.Filters ??= new List<GridFilter>();

        // Solo las filas con código (ReferenceId) son instancias canjeables; las demás son asientos del libro. Los
        // filtros de la página van sobre la tabla, antes de proyectar, para que usen las columnas tal cual.
        var query = _db.TokenTransactions
            .AsNoTracking()
            .Where(t => t.MemberId == request.MemberId && t.ReferenceId != null);

        if (request.TokenTypeId.HasValue)
            query = query.Where(t => t.TokenTypeId == request.TokenTypeId.Value);

        if (request.IsUsed.HasValue)
            query = request.IsUsed.Value
                ? query.Where(t => t.UsedAt != null)
                : query.Where(t => t.UsedAt == null);

        // Se proyecta a una fila con propiedades asignables y no directamente a TokenCodeDto: el DTO es un record
        // posicional y EF no sabe filtrar ni ordenar por una propiedad que solo se llena por el constructor. El tipo
        // de token sale por subconsulta para que su nombre también se pueda filtrar y ordenar en el servidor.
        var projected = query.Select(t => new TokenCodeGridRow
        {
            Id              = t.Id,
            TokenCode       = t.ReferenceId!,
            TokenTypeName   = _db.TokenTypes.Where(tt => tt.Id == t.TokenTypeId).Select(tt => tt.Name).FirstOrDefault() ?? string.Empty,
            IsGuestPass     = _db.TokenTypes.Where(tt => tt.Id == t.TokenTypeId).Select(tt => tt.IsGuestPass).FirstOrDefault(),
            TransactionType = t.TransactionType.ToString(),
            IsUsed          = t.UsedAt != null,
            UsedByMemberId  = t.UsedByMemberId,
            UsedAt          = t.UsedAt,
            CreatedDate     = t.CreationDate
        });

        var page = await projected.ToGridResultAsync(grid, SearchableFields, MaxPageSize, ct);

        // El contrato de la API sigue siendo TokenCodeDto: se traduce solo la página ya leída.
        return Result<PagedResult<TokenCodeDto>>.Success(new PagedResult<TokenCodeDto>
        {
            Items = page.Items.Select(r => new TokenCodeDto(
                r.Id, r.TokenCode, r.TokenTypeName, r.IsGuestPass, r.TransactionType,
                r.IsUsed, r.UsedByMemberId, r.UsedAt, r.CreatedDate)).ToList(),
            TotalCount = page.TotalCount,
            Page       = page.Page,
            PageSize   = page.PageSize
        });
    }
}

/// <summary>
/// Forma de <see cref="TokenCodeDto"/> con propiedades asignables, para que el grid filtre y ordene sobre ella en la
/// base. Mismos nombres que el DTO: los campos que manda la página valen para los dos.
/// </summary>
public sealed class TokenCodeGridRow
{
    public long      Id              { get; set; }
    public string    TokenCode       { get; set; } = string.Empty;
    public string    TokenTypeName   { get; set; } = string.Empty;
    public bool      IsGuestPass     { get; set; }
    public string    TransactionType { get; set; } = string.Empty;
    public bool      IsUsed          { get; set; }
    public string?   UsedByMemberId  { get; set; }
    public DateTime? UsedAt          { get; set; }
    public DateTime  CreatedDate     { get; set; }
}
