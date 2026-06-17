using Hangfire;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;

/// <summary>
/// Builds a fully-wired <see cref="EvaluateRankHandler"/> for reachability tests.
/// Side-effect dependencies (cache, background jobs, mediator) are no-op mocks.
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

    /// <summary>
    /// IBackgroundJobClient mock — Enqueue&lt;T&gt; is an extension that delegates to
    /// Create(...); we stub Create to return a fake job id so the extension is happy.
    /// </summary>
    private static Mock<IBackgroundJobClient> BuildJobs()
    {
        var m = new Mock<IBackgroundJobClient>();
        m.Setup(c => c.Create(
                It.IsAny<Hangfire.Common.Job>(),
                It.IsAny<Hangfire.States.IState>()))
            .Returns("test-job-id");
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
            BuildJobs().Object);
}
