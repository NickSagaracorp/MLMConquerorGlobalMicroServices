using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Features;

public class ComputeProgressTests
{
    private static RankRequirement BuildReq(
        int personalPoints   = 0,
        int teamPoints       = 0,
        int enrollmentTeam   = 0,
        int sponsoredMembers = 0,
        int externalMembers  = 0,
        decimal salesVolume  = 0) => new()
    {
        PersonalPoints   = personalPoints,
        TeamPoints       = teamPoints,
        EnrollmentTeam   = enrollmentTeam,
        SponsoredMembers = sponsoredMembers,
        ExternalMembers  = externalMembers,
        SalesVolume      = salesVolume,
        CreatedBy        = "seed",
        CreationDate     = DateTime.UtcNow
    };

    private static RankMetricsResponse BuildMetrics(
        int personalPoints    = 0,
        decimal teamPoints    = 0,
        int enrollmentTeam    = 0,
        int sponsoredMembers  = 0,
        int externalMembers   = 0,
        decimal salesVolume   = 0) => new()
    {
        PersonalPoints               = personalPoints,
        QualifyingTeamPoints         = teamPoints,
        EnrollmentTeamCount          = enrollmentTeam,
        SponsoredMembers             = sponsoredMembers,
        ExternalMembers              = externalMembers,
        SalesVolume                  = salesVolume
    };

    [Fact]
    public void ComputeProgress_WhenAllThresholdsZero_ReturnsHundredPercent()
    {
        var req     = BuildReq();
        var metrics = BuildMetrics();

        var progress = GetRankProgressHandler.ComputeProgress(metrics, req);

        progress.OverallPercent.Should().Be(100.0);
    }

    [Fact]
    public void ComputeProgress_WhenMeetsAllThresholds_ReturnsHundredPercent()
    {
        var req = BuildReq(personalPoints: 100, teamPoints: 500, sponsoredMembers: 2, externalMembers: 1);
        var metrics = BuildMetrics(personalPoints: 100, teamPoints: 500, sponsoredMembers: 2, externalMembers: 1);

        var progress = GetRankProgressHandler.ComputeProgress(metrics, req);

        progress.OverallPercent.Should().Be(100.0);
        progress.PersonalPointsPercent.Should().Be(100.0);
        progress.TeamPointsPercent.Should().Be(100.0);
    }

    [Fact]
    public void ComputeProgress_WhenAtHalfThreshold_ReturnsFiftyPercent()
    {
        var req     = BuildReq(personalPoints: 100);
        var metrics = BuildMetrics(personalPoints: 50);

        var progress = GetRankProgressHandler.ComputeProgress(metrics, req);

        progress.PersonalPointsPercent.Should().Be(50.0);
    }

    [Fact]
    public void ComputeProgress_WhenExceedsThreshold_CapsAtHundred()
    {
        var req     = BuildReq(personalPoints: 100);
        var metrics = BuildMetrics(personalPoints: 200);

        var progress = GetRankProgressHandler.ComputeProgress(metrics, req);

        progress.PersonalPointsPercent.Should().Be(100.0);
    }

    [Fact]
    public void ComputeProgress_OverallIsAverageOfActiveThresholdsOnly()
    {
        // Only personalPoints and sponsoredMembers have non-zero thresholds
        // personalPoints: 50/100 = 50%, sponsoredMembers: 1/2 = 50%
        // overall = (50 + 50) / 2 = 50%
        var req     = BuildReq(personalPoints: 100, sponsoredMembers: 2);
        var metrics = BuildMetrics(personalPoints: 50, sponsoredMembers: 1);

        var progress = GetRankProgressHandler.ComputeProgress(metrics, req);

        progress.OverallPercent.Should().Be(50.0);
    }

    [Fact]
    public void ComputeProgress_WhenZeroActualVsPositiveThreshold_ReturnsZeroPercent()
    {
        var req     = BuildReq(personalPoints: 100);
        var metrics = BuildMetrics(personalPoints: 0);

        var progress = GetRankProgressHandler.ComputeProgress(metrics, req);

        progress.PersonalPointsPercent.Should().Be(0.0);
    }
}
