using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Domain;

public class MembershipSubscriptionTests
{
    private static MembershipSubscription BuildSubscription() => new()
    {
        MemberId = "AMB-000001",
        MembershipLevelId = 2,
        SubscriptionStatus = MembershipStatus.Active,
        ChangeReason = SubscriptionChangeReason.New,
        StartDate = DateTime.UtcNow.AddDays(-30),
        CreatedBy = "seed",
        LastUpdateDate = DateTime.UtcNow
    };


    [Fact]
    public void ValidateChange_WhenUpgradeToSameLevel_ThrowsMembershipChangeNotAllowedException()
    {
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(20, 20, SubscriptionChangeReason.Upgrade);

        act.Should().Throw<MembershipChangeNotAllowedException>()
           .WithMessage("*same*");
    }

    [Fact]
    public void ValidateChange_WhenDowngradeToSameLevel_ThrowsMembershipChangeNotAllowedException()
    {
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(20, 20, SubscriptionChangeReason.Downgrade);

        act.Should().Throw<MembershipChangeNotAllowedException>()
           .WithMessage("*same*");
    }


    [Fact]
    public void ValidateChange_WhenUpgradeToHigherSortOrder_Succeeds()
    {
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(30, 20, SubscriptionChangeReason.Upgrade);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateChange_WhenUpgradeToLowerSortOrder_ThrowsMembershipChangeNotAllowedException()
    {
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(10, 20, SubscriptionChangeReason.Upgrade);

        act.Should().Throw<MembershipChangeNotAllowedException>()
           .WithMessage("*higher*");
    }

    [Fact]
    public void ValidateChange_WhenUpgradeToEqualSortOrder_ThrowsMembershipChangeNotAllowedException()
    {
        // newSortOrder <= current is invalid for upgrade (equal is also caught by <=)
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(20, 20, SubscriptionChangeReason.Upgrade);

        act.Should().Throw<MembershipChangeNotAllowedException>();
    }


    [Fact]
    public void ValidateChange_WhenDowngradeToLowerSortOrder_Succeeds()
    {
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(10, 20, SubscriptionChangeReason.Downgrade);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateChange_WhenDowngradeToHigherSortOrder_ThrowsMembershipChangeNotAllowedException()
    {
        var sub = BuildSubscription();

        Action act = () => sub.ValidateChange(30, 20, SubscriptionChangeReason.Downgrade);

        act.Should().Throw<MembershipChangeNotAllowedException>()
           .WithMessage("*lower*");
    }
}
