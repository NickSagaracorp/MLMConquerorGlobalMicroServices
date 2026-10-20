using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring.HighVolume;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>Plan Q (DQ5): la etapa 4 del motor de alto volumen encola rango solo a la linea cercana.</summary>
public class DownstreamTriggersTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 9, 0, 0);

    [Fact]
    public async Task Encola_rango_solo_a_los_10_ascendientes_mas_cercanos()
    {
        using var db = TestDbContextFactory.Create();
        var linea = Enumerable.Range(1, 15).Select(i => $"AMB-{i:D6}").ToArray();
        db.GenealogyTree.Add(new GenealogyEntity
        {
            Id = Guid.NewGuid().ToString(), MemberId = "AMB-NUEVO", ParentMemberId = linea[^1],
            HierarchyPath = "/" + string.Join("/", linea) + "/AMB-NUEVO/", Level = 16,
            CreatedBy = "test", CreationDate = Now, LastUpdateDate = Now
        });
        db.PointDeltaEvents.Add(new PointDeltaEvent
        {
            BatchId = "LOTE-1", OrderId = "ORD-1", MemberId = "AMB-NUEVO", EventType = PointDeltaEventType.Activated,
            OccurredAt = Now, Status = PointDeltaEventStatus.Applied, CreatedBy = "test", CreationDate = Now
        });
        await db.SaveChangesAsync();
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(d => d.Now).Returns(Now);
        var disparos = new DownstreamTriggers(db, reloj.Object, Mock.Of<IPushNotificationService>(),
            Mock.Of<IBackgroundJobClient>(), Mock.Of<ILogger<DownstreamTriggers>>());

        (await disparos.DispatchAsync("LOTE-1")).IsSuccess.Should().BeTrue();

        (await db.RankEvaluationQueue.Where(q => q.TriggerEvent == RankEvaluationTrigger.MembershipChange)
                .Select(q => q.EvaluateMemberId).ToListAsync())
            .Should().BeEquivalentTo(linea[^10..]);
    }
}
