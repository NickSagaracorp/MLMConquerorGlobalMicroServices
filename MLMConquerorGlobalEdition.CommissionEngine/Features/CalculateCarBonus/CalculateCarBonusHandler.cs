using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.CommissionEngine.DTOs;
using MLMConquerorGlobalEdition.CommissionEngine.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.CommissionEngine.Features.CalculateCarBonus;

public class CalculateCarBonusHandler : IRequestHandler<CalculateCarBonusCommand, Result<CalculationResultResponse>>
{
    // Comp plan: Elite=6 pts, Turbo=6 pts, VIP=1 pt per active subscription
    private static readonly Dictionary<int, int> LevelPoints = new() { [2] = 1, [3] = 6, [4] = 6 };

    private const int CarBonusTypeId     = 85;
    private const int CarBonusCategoryId = 4;

    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _dateTime;
    private readonly ICurrentUserService _currentUser;

    public CalculateCarBonusHandler(AppDbContext db, IDateTimeProvider dateTime, ICurrentUserService currentUser)
    {
        _db          = db;
        _dateTime    = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CalculationResultResponse>> Handle(
        CalculateCarBonusCommand command, CancellationToken ct)
    {
        var now = _dateTime.Now;

        // Resolve the period: first day of the target month
        var periodDate = command.PeriodDate.HasValue
            ? new DateTime(command.PeriodDate.Value.Year, command.PeriodDate.Value.Month, 1)
            : new DateTime(now.Year, now.Month, 1);
        var monthEnd = periodDate.AddMonths(1);

        // Load Car Bonus commission type
        var carBonusType = await _db.CommissionTypes
            .AsNoTracking()
            .Where(t => t.Id == CarBonusTypeId && t.IsActive && t.CommissionCategoryId == CarBonusCategoryId)
            .FirstOrDefaultAsync(ct);

        if (carBonusType is null)
            return Result<CalculationResultResponse>.Failure(
                "NO_CAR_BONUS_TYPE", "No active Car Bonus commission type configured (ID 85).");

        // Idempotency: skip if already calculated for this month
        var alreadyRan = await _db.CommissionEarnings
            .AnyAsync(e => e.CommissionTypeId == CarBonusTypeId
                        && e.PeriodDate.HasValue
                        && e.PeriodDate.Value >= periodDate
                        && e.PeriodDate.Value < monthEnd, ct);

        if (alreadyRan)
            return Result<CalculationResultResponse>.Failure(
                "ALREADY_CALCULATED",
                $"Car Bonus for {periodDate:yyyy-MM} was already calculated.");

        // Resolve all active ambassadors (membership level 3 or 4 = Elite/Turbo)
        var ambassadors = await (
            from sub in _db.MembershipSubscriptions.AsNoTracking()
            where sub.SubscriptionStatus == MembershipStatus.Active
               && !sub.IsDeleted
               && (sub.MembershipLevelId == 3 || sub.MembershipLevelId == 4)
            select sub.MemberId
        ).Distinct().ToListAsync(ct);

        if (ambassadors.Count == 0)
            return Result<CalculationResultResponse>.Success(new CalculationResultResponse
            {
                CommissionType        = "CarBonus",
                RecordsCreated        = 0,
                TotalAmountCalculated = 0,
                PeriodDate            = periodDate,
                SkippedReasons        = ["No active Elite/Turbo ambassadors found."]
            });

        // Plan M (revisión M6): el equipo de cada embajador es el rango de su Node en el árbol de
        // inscripción (nodo vigente de cada miembro, CurrentUnder), no HierarchyPath.Contains en memoria.
        // Un miembro con dos nodos (R8 del Plan J) cuenta una vez, por el vigente, y ya no revienta el
        // ToDictionary. Los puntos de cada miembro salen de su suscripción activa más reciente.
        var activos = await (
            from g   in _db.GenealogyTree.AsNoTracking().CurrentUnder(HierarchyId.GetRoot())
            join sub in _db.MembershipSubscriptions.AsNoTracking() on g.MemberId equals sub.MemberId
            where sub.SubscriptionStatus == MembershipStatus.Active && !sub.IsDeleted
            select new { g.MemberId, g.Node, sub.MembershipLevelId, sub.CreationDate }
        ).ToListAsync(ct);

        // Un registro por miembro, ordenado por Node: el equipo de un nodo es un tramo contiguo
        // [nodo, hermano siguiente) — exacto porque los ordinales son enteros (CHECK de NodoDeGenealogia).
        var porNodo = activos
            .GroupBy(a => a.MemberId)
            .Select(grupo =>
            {
                var ultima = grupo.OrderByDescending(a => a.CreationDate).First();
                return (ultima.MemberId, Node: ultima.Node!,
                        Puntos: LevelPoints.TryGetValue(ultima.MembershipLevelId, out var p) ? p : 0);
            })
            .OrderBy(x => x.Node)
            .ToList();
        var nodos     = porNodo.Select(x => x.Node).ToList();
        var acumulado = new int[porNodo.Count + 1];
        for (var i = 0; i < porNodo.Count; i++)
            acumulado[i + 1] = acumulado[i] + porNodo[i].Puntos;

        int Desde(HierarchyId n)
        {
            var i = nodos.BinarySearch(n);
            return i >= 0 ? i : ~i;
        }

        // El nodo vigente de cada embajador (pueden no tener suscripción activa propia en "activos").
        var nodoDe = await _db.GenealogyTree.AsNoTracking()
            .CurrentUnder(HierarchyId.GetRoot())
            .Where(g => ambassadors.Contains(g.MemberId))
            .Select(g => new { g.MemberId, g.Node })
            .ToListAsync(ct);
        var nodoPorEmbajador = nodoDe.GroupBy(x => x.MemberId).ToDictionary(x => x.Key, x => x.First().Node);

        var skipped   = new List<string>();
        var earnings  = new List<CommissionEarning>();
        var payDate   = new DateTime(periodDate.AddMonths(1).Year, periodDate.AddMonths(1).Month, 15);

        foreach (var ambassadorId in ambassadors)
        {
            // Verify ambassador's own personal points
            var ownSub = await _db.MembershipSubscriptions
                .AsNoTracking()
                .Where(s => s.MemberId == ambassadorId
                         && s.SubscriptionStatus == MembershipStatus.Active
                         && !s.IsDeleted)
                .OrderByDescending(s => s.CreationDate)
                .FirstOrDefaultAsync(ct);

            var personalPoints = ownSub is not null
                && LevelPoints.TryGetValue(ownSub.MembershipLevelId, out var pp) ? pp : 0;

            if (personalPoints < carBonusType.PersonalPoints)
            {
                skipped.Add($"{ambassadorId}: personal points {personalPoints} < required {carBonusType.PersonalPoints}.");
                continue;
            }

            if (!nodoPorEmbajador.TryGetValue(ambassadorId, out var raiz) || raiz is null)
            {
                // Falla cerrado: sin Node no hay equipo que sumar (relleno pendiente).
                skipped.Add($"{ambassadorId}: no enrollment Node (GenealogyTree.Node pending backfill).");
                continue;
            }

            // Calculate team enrollment points from the active downline: el tramo de su Node sin él.
            var padre     = raiz.GetAncestor(1);
            var siguiente = padre?.GetDescendant(raiz, null);
            var desde     = Desde(raiz);
            var hasta     = siguiente is null ? porNodo.Count : Desde(siguiente);
            var propios   = desde < porNodo.Count && porNodo[desde].Node == raiz ? porNodo[desde].Puntos : 0;
            var teamPoints = acumulado[hasta] - acumulado[desde] - propios;

            if (teamPoints < carBonusType.TeamPoints)
            {
                skipped.Add($"{ambassadorId}: team points {teamPoints} < required {carBonusType.TeamPoints}.");
                continue;
            }

            var amount = carBonusType.ActiveAmount ?? 500m;
            if (amount <= 0)
            {
                skipped.Add($"{ambassadorId}: Car Bonus amount is 0.");
                continue;
            }

            earnings.Add(new CommissionEarning
            {
                BeneficiaryMemberId = ambassadorId,
                CommissionTypeId    = CarBonusTypeId,
                Amount              = amount,
                Status              = CommissionEarningStatus.Pending,
                EarnedDate          = now,
                PaymentDate         = payDate,
                PeriodDate          = periodDate,
                Notes               = $"car-bonus-{periodDate:yyyy-MM}",
                CreatedBy           = _currentUser.UserId,
                CreationDate        = now,
                LastUpdateDate      = now
            });
        }

        if (earnings.Count > 0)
        {
            await _db.CommissionEarnings.AddRangeAsync(earnings, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<CalculationResultResponse>.Success(new CalculationResultResponse
        {
            CommissionType        = "CarBonus",
            RecordsCreated        = earnings.Count,
            TotalAmountCalculated = earnings.Sum(e => e.Amount),
            PeriodDate            = periodDate,
            SkippedReasons        = skipped
        });
    }
}
