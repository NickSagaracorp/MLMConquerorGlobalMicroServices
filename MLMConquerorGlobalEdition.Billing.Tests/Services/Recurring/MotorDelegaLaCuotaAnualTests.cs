using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Un solo motor (Plan K): RecurringBillingProcessor recibe los estados de cuota anual del planificador y
/// del barrido y los pasa a AnnualFeeChargeService. No crea pedidos de renovación ni toca suscripciones.
/// Un estado que mezcla propósito y plan no se cobra.
/// </summary>
public class MotorDelegaLaCuotaAnualTests
{
    private readonly Mock<IAnnualFeeChargeService> _cuota = new();

    private RecurringBillingProcessor Procesador(AppDbContext db) => new(
        db, new Mock<ICommissionBalanceService>().Object, new RecurringBillingScheduler(),
        new Mock<IGatewayRouter>().Object, new Mock<IGatewayChargeOrchestrator>().Object,
        new Mock<ICardBrandDetector>().Object, new TransactionOnlyGate(db), _cuota.Object,
        CuotaAnualDb.Reloj(), NullLogger<RecurringBillingProcessor>.Instance);

    private static async Task<AppDbContext> ConEstadoAsync(BillingStatePurpose proposito, bool planAnual)
    {
        var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        if (!planAnual)
        {
            var plan = await db.RecurringBillingPlans.SingleAsync();
            plan.IsAnnualFeePlan = false;
        }
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado", Purpose = proposito,
            MembershipSubscriptionId = proposito == BillingStatePurpose.Membership ? "sub-1" : null,
            MemberId = "AMB-1", RecurringBillingPlanId = CuotaAnualDb.PlanAnual, NextBillingDate = new DateTime(2027, 6, 5),
            NextAttemptDate = new DateTime(2027, 6, 5), Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Un_estado_de_cuota_anual_lo_cobra_el_servicio_de_la_cuota(bool forzado)
    {
        await using var db = await ConEstadoAsync(BillingStatePurpose.AnnualFee, planAnual: true);
        _cuota.Setup(c => c.ChargeDueAsync("estado", forzado, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result<AnnualFeeChargeOutcome>.Success(
                  new AnnualFeeChargeOutcome(AnnualFeeOutcomes.Success, "Token", "ord-1", null)));

        var r = await Procesador(db).ProcessAsync("estado", forzado);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Outcome.Should().Be("Success");
        r.Value.FundingSource.Should().Be("Token");
        r.Value.OrderId.Should().Be("ord-1");
        _cuota.Verify(c => c.ChargeDueAsync("estado", forzado, It.IsAny<CancellationToken>()), Times.Once);
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_fallo_del_servicio_de_la_cuota_llega_tal_cual()
    {
        await using var db = await ConEstadoAsync(BillingStatePurpose.AnnualFee, planAnual: true);
        _cuota.Setup(c => c.ChargeDueAsync("estado", false, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result<AnnualFeeChargeOutcome>.Failure("ANNUAL_FEE_BUSY", "ocupado"));

        var r = await Procesador(db).ProcessAsync("estado");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("ANNUAL_FEE_BUSY");
    }

    [Theory]
    [InlineData(BillingStatePurpose.Membership, true)]
    [InlineData(BillingStatePurpose.AnnualFee, false)]
    public async Task Un_estado_que_mezcla_proposito_y_plan_no_se_cobra(BillingStatePurpose proposito, bool planAnual)
    {
        await using var db = await ConEstadoAsync(proposito, planAnual);

        var r = await Procesador(db).ProcessAsync("estado");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be(RecurringBillingProcessor.AnnualFeeStateMismatchCode);
        _cuota.VerifyNoOtherCalls();
        (await db.Orders.CountAsync()).Should().Be(0);
    }
}
