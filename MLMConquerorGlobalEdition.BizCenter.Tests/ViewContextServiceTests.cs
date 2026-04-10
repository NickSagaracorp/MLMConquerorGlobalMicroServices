using Microsoft.AspNetCore.Http;
using MLMConquerorGlobalEdition.SharedComponents.Constants;
using MLMConquerorGlobalEdition.SharedComponents.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

public class ViewContextServiceTests
{
    private static ViewContextService CreateService() =>
        new(new HttpContextAccessor());


    [Fact]
    public void SetContext_WhenCalled_UpdatesAllProperties()
    {
        var svc = CreateService();
        svc.SetContext("mem-1", "usr-1", false, false, [AppRoles.Ambassador]);

        svc.ViewingMemberId.Should().Be("mem-1");
        svc.ViewerUserId.Should().Be("usr-1");
        svc.IsImpersonating.Should().BeFalse();
        svc.IsAdminContext.Should().BeFalse();
        svc.ViewerRoles.Should().Contain(AppRoles.Ambassador);
    }

    [Fact]
    public void SetContext_WhenCalledWithAdmin_SetsAdminContext()
    {
        var svc = CreateService();
        svc.SetContext("mem-2", "usr-2", false, true, [AppRoles.Admin]);

        svc.IsAdminContext.Should().BeTrue();
        svc.ViewerRoles.Should().Contain(AppRoles.Admin);
    }


    [Fact]
    public void HasPermission_WhenSuperAdmin_AlwaysReturnsTrue()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.SuperAdmin]);

        svc.HasPermission(Permissions.Commission.Delete).Should().BeTrue();
        svc.HasPermission(Permissions.Member.ChangeStatus).Should().BeTrue();
        svc.HasPermission(Permissions.GhostPoints.Add).Should().BeTrue();
        svc.HasPermission(Permissions.Rank.Override).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WhenAdmin_AlwaysReturnsTrue()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.Admin]);

        svc.HasPermission(Permissions.Commission.Delete).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.ViewAll).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WhenCommissionManager_CanDeleteAndForcePayCommission()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.CommissionManager]);

        svc.HasPermission(Permissions.Commission.Delete).Should().BeTrue();
        svc.HasPermission(Permissions.Commission.ForcePay).Should().BeTrue();
        svc.HasPermission(Permissions.Commission.View).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WhenBillingManager_CanViewWalletFullHistory()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.BillingManager]);

        svc.HasPermission(Permissions.Wallet.ViewFullHistory).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WhenSupportManager_CanAssignAndResolveTickets()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.SupportManager]);

        svc.HasPermission(Permissions.Ticket.Assign).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.Resolve).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.Merge).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.EscalateToL2).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.EscalateToL3).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.EscalateToIT).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_WhenSupportLevel1_CanEscalateToL2Only()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.SupportLevel1]);

        svc.HasPermission(Permissions.Ticket.EscalateToL2).Should().BeTrue();
        svc.HasPermission(Permissions.Ticket.EscalateToL3).Should().BeFalse();
        svc.HasPermission(Permissions.Ticket.EscalateToIT).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WhenSupportTicketRoles_CanViewAllTickets()
    {
        foreach (var role in AppRoles.SupportRoles)
        {
            var svc = CreateService();
            svc.SetContext("m", "u", false, true, [role]);
            svc.HasPermission(Permissions.Ticket.ViewAll).Should().BeTrue(
                because: $"role {role} should be able to view all tickets");
        }
    }


    [Fact]
    public void HasPermission_WhenAmbassador_CannotDeleteCommission()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, false, [AppRoles.Ambassador]);

        svc.HasPermission(Permissions.Commission.Delete).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WhenAmbassador_CannotViewWalletFullHistory()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, false, [AppRoles.Ambassador]);

        svc.HasPermission(Permissions.Wallet.ViewFullHistory).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WhenNoRoles_ReturnsFalseForAllPermissions()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, false, []);

        svc.HasPermission(Permissions.Commission.Delete).Should().BeFalse();
        svc.HasPermission(Permissions.Rank.Override).Should().BeFalse();
        svc.HasPermission(Permissions.SystemUsers.Manage).Should().BeFalse();
        svc.HasPermission(Permissions.Ticket.Assign).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_WhenCommissionManager_CannotOverrideRank()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.CommissionManager]);

        svc.HasPermission(Permissions.Rank.Override).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_SystemUsersManage_OnlyForSuperAdmin()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, true, [AppRoles.Admin]);

        svc.HasPermission(Permissions.SystemUsers.Manage).Should().BeFalse();
    }


    [Fact]
    public void IsInAnyRole_WhenMatchingRole_ReturnsTrue()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, false, [AppRoles.Ambassador]);

        svc.IsInAnyRole(AppRoles.Ambassador, AppRoles.Admin).Should().BeTrue();
    }

    [Fact]
    public void IsInAnyRole_WhenNoMatchingRole_ReturnsFalse()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, false, [AppRoles.Ambassador]);

        svc.IsInAnyRole(AppRoles.Admin, AppRoles.SuperAdmin).Should().BeFalse();
    }

    [Fact]
    public void IsInAnyRole_WhenEmptyRoles_ReturnsFalse()
    {
        var svc = CreateService();
        svc.SetContext("m", "u", false, false, []);

        svc.IsInAnyRole(AppRoles.Ambassador).Should().BeFalse();
    }
}
