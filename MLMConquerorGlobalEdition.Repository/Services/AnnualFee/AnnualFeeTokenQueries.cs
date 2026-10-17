using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Repository.Services.AnnualFee;

/// <summary>
/// Los tokens que pueden pagar la cuota de un miembro (Plan K, K8). La usan el BizCenter (para enseñar
/// cuántos tiene) y Billing (para gastar uno), así que los dos ven lo mismo.
/// </summary>
public static class AnnualFeeTokenQueries
{
    /// <summary>
    /// Instancias canjeables (con código) del miembro, de los tipos de cuota, vivas (Issued o Distributed:
    /// las mismas que MemberTokenQueries cuenta como disponibles) y sin caducar. Con seguimiento: quien
    /// gasta la modifica. Para contar, basta con CountAsync.
    /// </summary>
    public static IQueryable<TokenTransaction> Available(AppDbContext db, string memberId, DateTime now)
    {
        var types = AnnualFeeRules.FeeTokenTypeIds.ToArray();
        return db.TokenTransactions.Where(t =>
            t.MemberId == memberId
            && t.ReferenceId != null
            && types.Contains(t.TokenTypeId)
            && (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
            && (t.ExpiresAt == null || t.ExpiresAt > now));
    }

    /// <summary>El orden en que se gastan: "Annual: Biz Center" antes que "Annual Fee"; dentro, el más antiguo.</summary>
    public static IQueryable<TokenTransaction> InSpendingOrder(this IQueryable<TokenTransaction> query) =>
        query.OrderBy(t => t.TokenTypeId == AnnualFeeRules.AnnualBizCenterTokenTypeId ? 0 : 1)
             .ThenBy(t => t.CreationDate)
             .ThenBy(t => t.Id);
}
