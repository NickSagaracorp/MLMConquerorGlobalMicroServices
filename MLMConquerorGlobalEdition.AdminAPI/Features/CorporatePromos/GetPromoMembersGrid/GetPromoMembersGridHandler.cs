using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembersGrid;

/// <summary>
/// Grid de miembros de una promo con filtro, orden y paginación en el servidor. Una promo no tiene tope de altas
/// (una de un mes con un millón de miembros puede traer decenas de miles), y la página pedía 500 y filtraba solo
/// esas. Aquí la promo se resuelve en una sola consulta proyectada al DTO, así que filtrar por nombre, correo, nivel
/// o estado abarca a todos sus miembros.
/// </summary>
public class GetPromoMembersGridHandler
    : IRequestHandler<GetPromoMembersGridQuery, Result<PagedResult<PromoMemberDto>>>
{
    /// <summary>Tope de filas por página; la página de AdminWeb ofrece 25, 50 y 100.</summary>
    internal const int MaxPageSize = 100;

    private static readonly string[] SearchableFields =
    {
        nameof(PromoMemberDto.MemberId),
        nameof(PromoMemberDto.FullName),
        nameof(PromoMemberDto.Email),
    };

    private readonly AppDbContext _db;

    public GetPromoMembersGridHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<PromoMemberDto>>> Handle(
        GetPromoMembersGridQuery request, CancellationToken cancellationToken)
    {
        var promo = await _db.CorporatePromos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PromoId && !x.IsDeleted, cancellationToken);

        if (promo is null)
            return Result<PagedResult<PromoMemberDto>>.Failure("PROMO_NOT_FOUND", "Corporate promo not found.");

        var grid = request.Grid ?? new GridDataRequest();
        if (grid.Sorts is not { Count: > 0 })
            grid.Sorts = new List<GridSort> { new() { Field = nameof(PromoMemberDto.SignupDate), Direction = "desc" } };
        grid.Filters ??= new List<GridFilter>();

        var inicio = promo.StartDate;
        var fin    = promo.EndDate;

        // Miembro de la promo = tiene alguna suscripción creada dentro de su ventana (el mismo criterio que el GET).
        // Se parte del perfil y no de agrupar suscripciones: así el filtro por nombre o correo cae en columnas del
        // perfil, y el nivel, el estado y la fecha de alta salen por subconsultas que el grid también puede filtrar y
        // ordenar en la base.
        var projected = _db.MemberProfiles
            .AsNoTracking()
            .Where(m => _db.MembershipSubscriptions.Any(s =>
                s.MemberId == m.MemberId && s.CreationDate >= inicio && s.CreationDate <= fin))
            .Select(m => new PromoMemberDto
            {
                MemberId = m.MemberId,
                FullName = (m.FirstName + " " + m.LastName).Trim(),
                Email    = m.Email,

                // Nivel y estado de la suscripción más reciente del miembro, como hacía el GET con el nivel.
                MembershipLevel = _db.MembershipSubscriptions
                    .Where(s => s.MemberId == m.MemberId)
                    .OrderByDescending(s => s.CreationDate)
                    .Select(s => s.MembershipLevel != null ? s.MembershipLevel.Name : string.Empty)
                    .FirstOrDefault() ?? string.Empty,
                SubscriptionStatus = _db.MembershipSubscriptions
                    .Where(s => s.MemberId == m.MemberId)
                    .OrderByDescending(s => s.CreationDate)
                    .Select(s => s.SubscriptionStatus.ToString())
                    .FirstOrDefault() ?? string.Empty,

                // Fecha de alta en la promo: su primera suscripción dentro de la ventana.
                SignupDate = _db.MembershipSubscriptions
                    .Where(s => s.MemberId == m.MemberId && s.CreationDate >= inicio && s.CreationDate <= fin)
                    .Min(s => s.CreationDate)
            });

        var result = await projected.ToGridResultAsync(grid, SearchableFields, MaxPageSize, cancellationToken);
        return Result<PagedResult<PromoMemberDto>>.Success(result);
    }
}
