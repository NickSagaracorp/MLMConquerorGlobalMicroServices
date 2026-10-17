using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Base y siembra de las pruebas de la cuota anual (Plan K). Los métodos de siembra añaden y guardan.
/// El plan anual es el 2 y su producto "prod-amb" cuesta 99 al año, como en la base local.
/// </summary>
public static class CuotaAnualDb
{
    public static readonly DateTime Ahora = new(2027, 6, 10, 9, 0, 0);
    public const int PlanAnual = 2;
    public const string Producto = "prod-amb";

    public static AppDbContext Nueva(string? nombre = null, IInterceptor? interceptor = null)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nombre ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (interceptor is not null) b.AddInterceptors(interceptor);
        return new AppDbContext(b.Options);
    }

    public static IDateTimeProvider Reloj(DateTime? ahora = null)
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(c => c.Now).Returns(ahora ?? Ahora);
        m.Setup(c => c.UtcNow).Returns(ahora ?? Ahora);
        return m.Object;
    }

    public static async Task PlanAsync(AppDbContext db, decimal precio = 99m, bool activo = true)
    {
        db.Products.Add(new Product
        {
            Id = Producto, Name = "Lifestyle Ambassador", Description = "", ImageUrl = "", MembershipLevelId = 1,
            AnnualPrice = precio, MonthlyFee = 0m, SetupFee = 99m, IsActive = true, CreatedBy = "prueba"
        });
        var plan = new RecurringBillingPlan
        {
            Id = PlanAnual, Name = "Lifestyle Ambassador (Annual)", CycleType = RecurringCycleType.AnnualFromLastBilling,
            RetryCadenceDays = "1,1,1,2,2,5,5", OnAllRetriesFail = RecurringFailurePolicy.MarkExpired,
            PayFromCommissionBalanceFirst = true, TokenTypeId = 103, IsActive = activo, IsAnnualFeePlan = true,
            CreatedBy = "prueba"
        };
        plan.PlanProducts.Add(new RecurringBillingPlanProduct { ProductId = Producto, CreatedBy = "prueba" });
        db.RecurringBillingPlans.Add(plan);
        await db.SaveChangesAsync();
    }

    public static async Task<MemberProfile> MiembroAsync(
        AppDbContext db, string memberId, DateTime? alta = null,
        MemberAccountStatus estado = MemberAccountStatus.Active, MemberType tipo = MemberType.Ambassador,
        bool mantener = false, bool conSaldo = true, bool borrado = false)
    {
        var p = new MemberProfile
        {
            UserId = Guid.NewGuid(), MemberId = memberId, Email = $"{memberId.ToLowerInvariant()}@prueba.test",
            FirstName = "Nombre", LastName = memberId, Country = "US", MemberType = tipo, Status = estado,
            EnrollDate = alta ?? new DateTime(2026, 6, 5, 14, 30, 0), KeepQualified = mantener,
            FeesFromBalance = conSaldo, IsDeleted = borrado, CreatedBy = "prueba"
        };
        db.MemberProfiles.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public static async Task<SubscriptionBillingState> EstadoAsync(
        AppDbContext db, string memberId, DateTime? vence = null,
        RecurringBillingStatus estado = RecurringBillingStatus.Active, DateTime? ultimoCobro = null)
    {
        var s = new SubscriptionBillingState
        {
            Id = "anual-" + memberId, Purpose = BillingStatePurpose.AnnualFee, MembershipSubscriptionId = null,
            MemberId = memberId, RecurringBillingPlanId = PlanAnual, BillingAnchorDate = new DateTime(2026, 6, 5),
            NextBillingDate = vence ?? new DateTime(2027, 6, 5), NextAttemptDate = vence ?? new DateTime(2027, 6, 5),
            LastSuccessfulBillingDate = ultimoCobro, Status = estado, CreatedBy = "prueba", CreationDate = Ahora
        };
        db.SubscriptionBillingStates.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    /// <summary>Comisión pendiente y ya pagable (PaymentDate en el pasado): saldo gastable.</summary>
    public static async Task ComisionAsync(AppDbContext db, string memberId, decimal importe, DateTime? pagable = null)
    {
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = memberId, CommissionTypeId = 1, Amount = importe,
            Status = CommissionEarningStatus.Pending, EarnedDate = Ahora.AddDays(-10),
            PaymentDate = pagable ?? Ahora.AddDays(-1), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Una instancia de token canjeable (con código) y el saldo agregado de su tipo.</summary>
    public static async Task<TokenTransaction> TokenAsync(
        AppDbContext db, string memberId, int tipo = AnnualFeeRules.AnnualBizCenterTokenTypeId, string? codigo = null,
        DateTime? creado = null, TokenInstanceStatus estado = TokenInstanceStatus.Issued, DateTime? caduca = null)
    {
        var t = new TokenTransaction
        {
            MemberId = memberId, TokenTypeId = tipo, TransactionType = TokenTransactionType.Purchased, Quantity = 1,
            ReferenceId = codigo ?? $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant(), OriginalOwnerMemberId = memberId,
            Status = estado, ExpiresAt = caduca, CreatedBy = "prueba", CreationDate = creado ?? Ahora.AddDays(-5)
        };
        db.TokenTransactions.Add(t);
        var saldo = await db.TokenBalances.FirstOrDefaultAsync(b => b.MemberId == memberId && b.TokenTypeId == tipo);
        if (saldo is null)
            db.TokenBalances.Add(new TokenBalance { MemberId = memberId, TokenTypeId = tipo, Balance = 1, CreatedBy = "prueba" });
        else
            saldo.Balance += 1;
        await db.SaveChangesAsync();

        // AuditInterceptor pone CreationDate = ahora al insertar; la fecha pedida se fija después.
        t.CreationDate = creado ?? Ahora.AddDays(-5);
        await db.SaveChangesAsync();
        return t;
    }
}
