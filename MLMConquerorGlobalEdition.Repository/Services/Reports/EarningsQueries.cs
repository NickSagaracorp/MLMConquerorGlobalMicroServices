using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Repository.Services.Reports;

/// <summary>Total de ganancias de un miembro (lo que devuelve <see cref="EarningsQueries.TotalsByMember"/>).</summary>
public sealed class MemberEarningsTotal
{
    public string  MemberId { get; init; } = string.Empty;
    public decimal Total    { get; init; }
}

/// <summary>
/// La regla de ganancias de los reportes del admin (Earnings Lifetime, Earnings por ventana y Club Recognition): suma de
/// <c>CommissionEarning.Amount</c> por beneficiario, solo Paid y Pending (las Cancelled no cuentan; las borradas tampoco,
/// por el filtro global), con <c>EarnedDate</c> en [<paramref name="from"/>, <paramref name="to"/>). Los residuales diarios
/// cuentan cuando se consolidan en CommissionEarning. Usa el índice IX_CommissionEarnings_EarnedDate_Beneficiary.
/// </summary>
public static class EarningsQueries
{
    public static IQueryable<MemberEarningsTotal> TotalsByMember(AppDbContext db, DateTime? from, DateTime to)
    {
        var earnings = db.CommissionEarnings.AsNoTracking()
            .Where(c => (c.Status == CommissionEarningStatus.Paid || c.Status == CommissionEarningStatus.Pending)
                     && c.EarnedDate < to);
        if (from is not null) earnings = earnings.Where(c => c.EarnedDate >= from);

        return earnings.GroupBy(c => c.BeneficiaryMemberId)
                       .Select(g => new MemberEarningsTotal { MemberId = g.Key, Total = g.Sum(c => c.Amount) });
    }
}
