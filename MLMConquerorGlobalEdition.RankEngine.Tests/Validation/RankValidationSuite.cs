using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.RankEngine.Features.EvaluateRank;
using MLMConquerorGlobalEdition.RankEngine.Tests.Helpers;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Validation;

/// <summary>
/// End-to-end rank regression: validate every rank against the real EvaluateRankHandler,
/// document the result, then purge the test data. All 19 ranks run against ONE shared
/// in-memory database so the purge step operates on real, persisted rows.
/// </summary>
public sealed class RankValidationSuite : IAsyncDisposable
{
    private readonly AppDbContext _db = InMemoryDbHelper.Create("rank-validation-suite");

    /// <summary>Builds and evaluates a scenario for every rank 1..19; returns the per-rank report.</summary>
    public async Task<RankValidationReport> RunRegressionAsync(CancellationToken ct = default)
    {
        var report = new RankValidationReport();
        var builder = new RankScenarioBuilder(_db);

        for (var rankId = 1; rankId <= 19; rankId++)
        {
            var subjectId = await builder.BuildForRankAsync(rankId);
            var result = await RankReachabilityTestHandlerFactory.Build(_db)
                .Handle(new EvaluateRankCommand(subjectId), ct);

            var rankName = (await _db.RankDefinitions.AsNoTracking()
                .FirstAsync(r => r.Id == rankId, ct)).Name;

            report.Rows.Add(new RankValidationRow
            {
                RankDefinitionId = rankId,
                RankName = rankName,
                Reached = result.IsSuccess
                          && result.Value!.RankAchieved
                          && result.Value.AchievedRank!.Id == rankId,
                AchievedRankId = result.Value?.AchievedRank?.Id ?? 0
            });
        }

        return report;
    }

    /// <summary>Writes the report markdown to docs/superpowers/rank-validation-report.md.</summary>
    public void WriteReport(RankValidationReport report)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.WriteLine("RankValidationSuite.WriteReport: repository root (CLAUDE.md) not found — report not written.");
            return;
        }
        var path = Path.Combine(repoRoot, "docs", "superpowers", "rank-validation-report.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, report.ToMarkdown());
    }

    /// <summary>
    /// Deletes every rank-validation-tagged row from the shared database and returns the
    /// count of surviving subject members (must be 0). Called after the result is documented.
    /// </summary>
    public async Task<int> PurgeValidationDataAsync(CancellationToken ct = default)
    {
        const string tag = RankScenarioBuilder.Tag;

        _db.MemberProfiles.RemoveRange(
            _db.MemberProfiles.Where(m => m.MemberId.StartsWith(RankScenarioBuilder.MemberPrefix)));
        _db.GenealogyTree.RemoveRange(_db.GenealogyTree.Where(g => g.CreatedBy == tag));
        _db.DualTeamTree.RemoveRange(_db.DualTeamTree.Where(d => d.CreatedBy == tag));
        _db.MemberStatistics.RemoveRange(_db.MemberStatistics.Where(s => s.CreatedBy == tag));
        _db.MembershipSubscriptions.RemoveRange(_db.MembershipSubscriptions.Where(s => s.CreatedBy == tag));
        _db.Orders.RemoveRange(_db.Orders.Where(o => o.CreatedBy == tag));
        _db.OrderDetails.RemoveRange(_db.OrderDetails.Where(o => o.CreatedBy == tag));
        _db.Products.RemoveRange(_db.Products.Where(p => p.CreatedBy == tag));
        _db.MemberRankHistories.RemoveRange(_db.MemberRankHistories.Where(h => h.CreatedBy == tag));
        await _db.SaveChangesAsync(ct);

        return await _db.MemberProfiles
            .CountAsync(m => m.MemberId.StartsWith(RankScenarioBuilder.MemberPrefix), ct);
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;
        return dir?.FullName;
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();
}
