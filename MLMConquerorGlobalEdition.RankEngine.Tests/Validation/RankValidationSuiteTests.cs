namespace MLMConquerorGlobalEdition.RankEngine.Tests.Validation;

public class RankValidationSuiteTests
{
    [Fact]
    public async Task RunRegression_AllNineteenRanksReachable_ThenPurgeRemovesEveryTaggedRow()
    {
        await using var suite = new RankValidationSuite();

        var report = await suite.RunRegressionAsync();

        report.Rows.Should().HaveCount(19);
        report.AllPassed.Should().BeTrue(report.ToMarkdown());

        // Document the successful result, then delete all test data.
        suite.WriteReport(report);
        var survivors = await suite.PurgeValidationDataAsync();
        survivors.Should().Be(0, "no rank-validation rows may survive cleanup");
    }
}
