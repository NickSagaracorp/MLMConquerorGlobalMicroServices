using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Commissions.GetCommissionStats;

/// <summary>
/// Contadores por estado de toda la tabla de comisiones para la cabecera de la pantalla. Un solo GROUP BY Status, que
/// SQL Server puede resolver leyendo un índice estrecho que ya incluye Status, y se guarda unos minutos en caché para
/// que abrir la pantalla no repita el agrupado de 2,4 millones de filas. Va aparte del grid a propósito: paginar,
/// ordenar o filtrar no debe recalcularlo.
/// </summary>
public class GetCommissionStatsHandler : IRequestHandler<GetCommissionStatsQuery, Result<CommissionStatsDto>>
{
    private readonly AppDbContext  _db;
    private readonly ICacheService _cache;

    public GetCommissionStatsHandler(AppDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<Result<CommissionStatsDto>> Handle(GetCommissionStatsQuery request, CancellationToken cancellationToken)
    {
        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<CommissionStatsDto>(CacheKeys.AdminCommissionStats, cancellationToken);
            if (cached is not null) return Result<CommissionStatsDto>.Success(cached);
        }

        var porEstado = await _db.CommissionEarnings
            .AsNoTracking()
            .GroupBy(c => c.Status)
            .Select(g => new { Estado = g.Key, Cuantas = g.Count() })
            .ToListAsync(cancellationToken);

        int De(CommissionEarningStatus e) => porEstado.Where(x => x.Estado == e).Sum(x => x.Cuantas);

        var stats = new CommissionStatsDto
        {
            Total     = porEstado.Sum(x => x.Cuantas),
            Pending   = De(CommissionEarningStatus.Pending),
            Paid      = De(CommissionEarningStatus.Paid),
            Cancelled = De(CommissionEarningStatus.Cancelled)
        };

        await _cache.SetAsync(CacheKeys.AdminCommissionStats, stats, CacheKeys.AdminCommissionStatsTtl, cancellationToken);
        return Result<CommissionStatsDto>.Success(stats);
    }
}
