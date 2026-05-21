using MediatR;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IEmailService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

/// <summary>
/// Builds a fully-wired <see cref="EvaluateRankHandler"/> for reachability tests.
/// All side-effect dependencies (cache, push, email, mediator) are no-op mocks.
/// The qualification service is real so it exercises actual rank logic against the in-memory DB.
/// </summary>
public static class RankReachabilityTestHandlerFactory
{
    private static readonly DateTime FixedNow = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<ICurrentUserService> BuildUser()
    {
        // EvaluateRankHandler only reads UserId (for MemberRankHistory.CreatedBy); other members are left default.
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("rank-validation");
        return m;
    }

    private static Mock<ICacheService> BuildCache()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IPushNotificationService> BuildPush()
    {
        var m = new Mock<IPushNotificationService>();
        m.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IEmailService> BuildEmail()
    {
        var m = new Mock<IEmailService>();
        m.Setup(e => e.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<ISender> BuildMediator()
    {
        var m = new Mock<ISender>();
        m.Setup(s => s.Send(
                It.IsAny<GenerateCertificateCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SharedKernel.Result<DTOs.CertificateGenerationResponse>.Success(
                new DTOs.CertificateGenerationResponse()));
        return m;
    }

    private static IRankQualificationService BuildQualification(AppDbContext db)
    {
        var et = new EnrollmentTeamPointsService(db);
        var pcp = new PersonalCustomerPointsService(db);
        return new RankQualificationService(db, et, pcp);
    }

    /// <summary>
    /// Constructs an <see cref="EvaluateRankHandler"/> wired against the supplied <paramref name="db"/>.
    /// </summary>
    public static EvaluateRankHandler Build(AppDbContext db) =>
        new(
            db,
            BuildClock().Object,
            BuildUser().Object,
            BuildQualification(db),
            BuildCache().Object,
            BuildPush().Object,
            BuildEmail().Object,
            BuildMediator().Object);
}
