using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// «¿Pagó su anualidad?» de verdad (Plan K, K16): el estado de cuota anual del miembro está al día, es
/// decir, su siguiente vencimiento es posterior a hoy (hora de servidor) y no está parado. Un estado
/// vencido sin pagar, parado o con el pago a conciliar (el estado no avanzó) no está al día. Sustituye a
/// AnnualFeeNotModeled, que siempre decía que no.
/// </summary>
/// <remarks>
/// Un estado recién creado para un Ambassador antiguo vence en su próximo aniversario y cuenta como al
/// día hasta entonces, aunque no haya pagado en este sistema (no se migran datos del legado). Pregunta
/// P1 del Plan K: si Nick prefiere exigir un cobro registrado, basta con añadir
/// <c>s.LastSuccessfulBillingDate != null</c> a la consulta.
/// </remarks>
public sealed class BillingStateAnnualFeeStatus : IAnnualFeeStatus
{
    private static readonly IReadOnlySet<string> Nobody = new HashSet<string>();

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public BillingStateAnnualFeeStatus(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    public async Task<IReadOnlySet<string>> PaidCurrentAnnualFeeAsync(
        IReadOnlyCollection<string> memberIds, CancellationToken ct = default)
    {
        var ids = memberIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (ids.Count == 0) return Nobody;

        var tomorrow = _clock.Now.Date.AddDays(1);
        var paid = await _db.SubscriptionBillingStates.AsNoTracking()
            .Where(s => s.Purpose == BillingStatePurpose.AnnualFee
                     && ids.Contains(s.MemberId)
                     && s.Status != RecurringBillingStatus.Stopped
                     && s.NextBillingDate >= tomorrow)
            .Select(s => s.MemberId)
            .ToListAsync(ct);

        return paid.ToHashSet(StringComparer.Ordinal);
    }
}
