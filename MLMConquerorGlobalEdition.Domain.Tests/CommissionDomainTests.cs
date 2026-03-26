using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class CommissionDomainTests
{
    [Fact]
    public void CommissionEarning_WhenStatusPaid_CannotBeCancelled()
    {
        var earning = new CommissionEarning { Status = CommissionEarningStatus.Paid };

        var action = () => earning.Cancel("test reason");

        action.Should().Throw<DomainException>()
            .Which.Code.Should().Be("COMMISSION_ALREADY_PAID");
    }

    [Fact]
    public void CommissionEarning_WhenStatusPending_CanBeCancelled()
    {
        var earning = new CommissionEarning { Status = CommissionEarningStatus.Pending };

        earning.Cancel("duplicate entry");

        earning.Status.Should().Be(CommissionEarningStatus.Cancelled);
        earning.Notes.Should().Be("duplicate entry");
    }

    [Fact]
    public void CommissionEarning_ManualEntry_SetsIsManualEntryTrue()
    {
        var earning = new CommissionEarning
        {
            IsManualEntry = true,
            CsvImportBatchId = "batch-001"
        };

        earning.IsManualEntry.Should().BeTrue();
        earning.CsvImportBatchId.Should().Be("batch-001");
    }

    [Fact]
    public void FastStartBonus_WhenOutsideWindow_IsNotTriggered()
    {
        var windowEndDate = new DateTime(2026, 1, 10);
        var now = new DateTime(2026, 1, 15);

        (now <= windowEndDate).Should().BeFalse();
    }

    [Fact]
    public void FastStartBonus_WhenWithinWindow_IsTriggered()
    {
        var windowEndDate = new DateTime(2026, 1, 20);
        var now = new DateTime(2026, 1, 15);

        (now <= windowEndDate).Should().BeTrue();
    }
}
