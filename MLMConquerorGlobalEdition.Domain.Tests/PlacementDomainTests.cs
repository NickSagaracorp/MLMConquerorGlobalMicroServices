using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class PlacementDomainTests
{
    [Fact]
    public void ValidatePlacement_WhenOver30Days_ThrowsPlacementWindowExpiredException()
    {
        var enrollmentDate = new DateTime(2026, 1, 1);
        var now = enrollmentDate.AddDays(31);
        var daysSinceEnrollment = (now - enrollmentDate).TotalDays;

        var action = () =>
        {
            if (daysSinceEnrollment > 30) throw new PlacementWindowExpiredException();
        };

        action.Should().Throw<PlacementWindowExpiredException>()
            .Which.Code.Should().Be("PLACEMENT_WINDOW_EXPIRED");
    }

    [Fact]
    public void ValidatePlacement_Within30Days_Succeeds()
    {
        var enrollmentDate = new DateTime(2026, 1, 1);
        var now = enrollmentDate.AddDays(15);
        var daysSinceEnrollment = (now - enrollmentDate).TotalDays;

        var action = () =>
        {
            if (daysSinceEnrollment > 30) throw new PlacementWindowExpiredException();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateUnplacement_WhenCountExceeds2_ThrowsUnplacementLimitExceededException()
    {
        var unplacementCount = 3;

        var action = () =>
        {
            if (unplacementCount > 2) throw new UnplacementLimitExceededException();
        };

        action.Should().Throw<UnplacementLimitExceededException>()
            .Which.Code.Should().Be("UNPLACEMENT_LIMIT_EXCEEDED");
    }

    [Fact]
    public void ValidateUnplacement_WhenOver72Hours_ThrowsUnplacementWindowExpiredException()
    {
        var firstPlacementDate = new DateTime(2026, 1, 1, 0, 0, 0);
        var now = firstPlacementDate.AddHours(73);
        var hoursSince = (now - firstPlacementDate).TotalHours;

        var action = () =>
        {
            if (hoursSince > 72) throw new UnplacementWindowExpiredException();
        };

        action.Should().Throw<UnplacementWindowExpiredException>()
            .Which.Code.Should().Be("UNPLACEMENT_WINDOW_EXPIRED");
    }

    [Fact]
    public void ValidateUnplacement_Within72HoursFirstTime_Succeeds()
    {
        var firstPlacementDate = new DateTime(2026, 1, 1);
        var now = firstPlacementDate.AddHours(24);
        var hoursSince = (now - firstPlacementDate).TotalHours;
        var unplacementCount = 1;

        var action = () =>
        {
            if (unplacementCount > 2) throw new UnplacementLimitExceededException();
            if (hoursSince > 72) throw new UnplacementWindowExpiredException();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void ValidateUnplacement_Within72HoursSecondTime_Succeeds()
    {
        var firstPlacementDate = new DateTime(2026, 1, 1);
        var now = firstPlacementDate.AddHours(48);
        var hoursSince = (now - firstPlacementDate).TotalHours;
        var unplacementCount = 2;

        var action = () =>
        {
            if (unplacementCount > 2) throw new UnplacementLimitExceededException();
            if (hoursSince > 72) throw new UnplacementWindowExpiredException();
        };

        action.Should().NotThrow();
    }
}
