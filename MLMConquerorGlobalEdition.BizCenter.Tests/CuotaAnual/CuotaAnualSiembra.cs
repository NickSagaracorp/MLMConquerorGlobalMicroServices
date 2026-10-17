using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Siembra de las pruebas de la cuota anual del lado del BizCenter (Plan K). Solo añade al contexto: quien
/// la usa guarda. El reloj es el de SharedKernel (RelojFijo), el que reciben los servicios de Repository.
/// </summary>
public static class CuotaAnualSiembra
{
    public static readonly DateTime Ahora = new(2027, 6, 10, 9, 0, 0);
    public static RelojFijo Reloj() => new(Ahora);

    public static void Plan(AppDbContext db, decimal precio = 99m)
    {
        db.Products.Add(new Product
        {
            Id = "prod-amb", Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1,
            MonthlyFee = 0m, SetupFee = 99m, AnnualPrice = precio, IsActive = true, CreatedBy = "prueba"
        });
        var plan = new RecurringBillingPlan
        {
            Id = 2, Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            IsActive = true, IsAnnualFeePlan = true, CreatedBy = "prueba"
        };
        plan.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = "prod-amb", CreatedBy = "prueba" });
        db.RecurringBillingPlans.Add(plan);
    }

    public static SubscriptionBillingState Estado(
        AppDbContext db, string memberId, DateTime vence, RecurringBillingStatus estado = RecurringBillingStatus.Active,
        BillingStatePurpose proposito = BillingStatePurpose.AnnualFee, DateTime? ultimoCobro = null)
    {
        var s = new SubscriptionBillingState
        {
            Id = $"estado-{memberId}-{proposito}", Purpose = proposito,
            MembershipSubscriptionId = proposito == BillingStatePurpose.Membership ? "sub-" + memberId : null,
            MemberId = memberId, RecurringBillingPlanId = 2, BillingAnchorDate = vence.AddYears(-1),
            NextBillingDate = vence, NextAttemptDate = vence, LastSuccessfulBillingDate = ultimoCobro,
            Status = estado, CreatedBy = "prueba"
        };
        db.SubscriptionBillingStates.Add(s);
        return s;
    }

    public static AnnualFeePayment Pago(
        AppDbContext db, string memberId, string estadoId, DateTime ciclo, AnnualFeePaymentStatus estado, DateTime creado,
        AnnualFeePaymentMethod metodo = AnnualFeePaymentMethod.CommissionBalance, decimal importe = 99m)
    {
        var p = new AnnualFeePayment
        {
            MemberId = memberId, SubscriptionBillingStateId = estadoId, CycleDueDate = ciclo, Amount = importe,
            Method = metodo, Source = AnnualFeePaymentSource.PayNow, Status = estado, OrderId = Guid.NewGuid().ToString(),
            CreatedBy = "prueba", CreationDate = creado,
            CompletedAt = estado == AnnualFeePaymentStatus.Completed ? creado : null
        };
        db.AnnualFeePayments.Add(p);
        return p;
    }

    public static void Token(AppDbContext db, string memberId, int tipo = AnnualFeeRules.AnnualBizCenterTokenTypeId,
        TokenInstanceStatus estado = TokenInstanceStatus.Issued) =>
        db.TokenTransactions.Add(new TokenTransaction
        {
            MemberId = memberId, TokenTypeId = tipo, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant(), OriginalOwnerMemberId = memberId,
            Status = estado, CreatedBy = "prueba", CreationDate = Ahora.AddDays(-3)
        });
}
