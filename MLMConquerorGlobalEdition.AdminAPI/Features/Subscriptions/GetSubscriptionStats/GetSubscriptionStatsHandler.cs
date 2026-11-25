using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Subscriptions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Subscriptions.GetSubscriptionStats;

public record GetSubscriptionStatsQuery(bool BypassCache = false) : IRequest<Result<SubscriptionStatsDto>>;

/// <summary>
/// Contadores de la cabecera de Subscriptions sobre toda la tabla. Se guardan en cache unos minutos para no repetir
/// cuatro COUNT sobre un millon de filas en cada carga; Refresh los salta.
/// </summary>
public class GetSubscriptionStatsHandler : IRequestHandler<GetSubscriptionStatsQuery, Result<SubscriptionStatsDto>>
{
    private readonly AppDbContext  _db;
    private readonly ICacheService _cache;

    public GetSubscriptionStatsHandler(AppDbContext db, ICacheService cache)
    {
        _db    = db;
        _cache = cache;
    }

    public async Task<Result<SubscriptionStatsDto>> Handle(GetSubscriptionStatsQuery request, CancellationToken ct)
    {
        if (!request.BypassCache)
        {
            var cached = await _cache.GetAsync<SubscriptionStatsDto>(CacheKeys.AdminSubscriptionStats, ct);
            if (cached is not null) return Result<SubscriptionStatsDto>.Success(cached);
        }

        // Hora del servidor (no UTC): las fechas de fin se guardan en hora de negocio.
        var limite = DateTime.Now.AddDays(7);
        var subs   = _db.MembershipSubscriptions.AsNoTracking();

        var stats = new SubscriptionStatsDto
        {
            Active          = await subs.CountAsync(s => s.SubscriptionStatus == MembershipStatus.Active, ct),
            ExpiringIn7Days = await subs.CountAsync(s => s.SubscriptionStatus == MembershipStatus.Active
                                                         && s.EndDate != null && s.EndDate <= limite, ct),
            Cancelled       = await subs.CountAsync(s => s.SubscriptionStatus == MembershipStatus.Cancelled, ct),
            AutoRenew       = await subs.CountAsync(s => s.IsAutoRenew, ct),
        };

        await _cache.SetAsync(CacheKeys.AdminSubscriptionStats, stats, CacheKeys.AdminSubscriptionStatsTtl, ct);
        return Result<SubscriptionStatsDto>.Success(stats);
    }
}
