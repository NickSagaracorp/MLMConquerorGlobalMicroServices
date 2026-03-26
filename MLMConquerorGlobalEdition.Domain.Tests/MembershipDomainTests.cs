using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class MembershipDomainTests
{
    [Fact]
    public void MembershipUpgrade_WhenHigherSortOrder_Succeeds()
    {
        var subscription = new MembershipSubscription { MembershipLevelId = 1 };

        var action = () => subscription.ValidateChange(3, 1, SubscriptionChangeReason.Upgrade);

        action.Should().NotThrow();
    }

    [Fact]
    public void MembershipDowngrade_WhenLowerSortOrder_Succeeds()
    {
        var subscription = new MembershipSubscription { MembershipLevelId = 3 };

        var action = () => subscription.ValidateChange(1, 3, SubscriptionChangeReason.Downgrade);

        action.Should().NotThrow();
    }

    [Fact]
    public void MembershipChange_WhenSameLevelRequested_ThrowsMembershipChangeNotAllowedException()
    {
        var subscription = new MembershipSubscription { MembershipLevelId = 2 };

        var action = () => subscription.ValidateChange(2, 2, SubscriptionChangeReason.Upgrade);

        action.Should().Throw<MembershipChangeNotAllowedException>()
            .Which.Code.Should().Be("MEMBERSHIP_CHANGE_NOT_ALLOWED");
    }

    [Fact]
    public void MembershipUpgrade_WhenLowerSortOrderProvided_ThrowsMembershipChangeNotAllowedException()
    {
        var subscription = new MembershipSubscription { MembershipLevelId = 3 };

        var action = () => subscription.ValidateChange(1, 3, SubscriptionChangeReason.Upgrade);

        action.Should().Throw<MembershipChangeNotAllowedException>()
            .Which.Code.Should().Be("MEMBERSHIP_CHANGE_NOT_ALLOWED");
    }

    [Fact]
    public void MembershipDowngrade_WhenHigherSortOrderProvided_ThrowsMembershipChangeNotAllowedException()
    {
        var subscription = new MembershipSubscription { MembershipLevelId = 1 };

        var action = () => subscription.ValidateChange(3, 1, SubscriptionChangeReason.Downgrade);

        action.Should().Throw<MembershipChangeNotAllowedException>()
            .Which.Code.Should().Be("MEMBERSHIP_CHANGE_NOT_ALLOWED");
    }
}
