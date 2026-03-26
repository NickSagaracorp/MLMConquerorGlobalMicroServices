using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class WalletDomainTests
{
    [Fact]
    public void WalletPassword_WhenStoredInPlainText_ThrowsWalletPasswordStorageException()
    {
        var wallet = new MemberProfilesWallet { WalletType = WalletType.eWallet };

        var action = () => wallet.SetEWalletPassword("mypassword123");

        action.Should().Throw<WalletPasswordStorageException>()
            .Which.Code.Should().Be("WALLET_PASSWORD_NOT_ENCRYPTED");
    }

    [Fact]
    public void WalletPassword_WhenEmpty_ThrowsWalletPasswordStorageException()
    {
        var wallet = new MemberProfilesWallet { WalletType = WalletType.eWallet };

        var action = () => wallet.SetEWalletPassword(string.Empty);

        action.Should().Throw<WalletPasswordStorageException>()
            .Which.Code.Should().Be("WALLET_PASSWORD_NOT_ENCRYPTED");
    }

    [Fact]
    public void WalletPassword_WhenEncrypted_SetsPassword()
    {
        var wallet = new MemberProfilesWallet { WalletType = WalletType.eWallet };
        var encrypted = "ENC:AES256:abc123xyz==";

        wallet.SetEWalletPassword(encrypted);

        wallet.eWalletPasswordEncrypted.Should().Be(encrypted);
    }

    [Fact]
    public void WalletHistory_WhenWalletUpdated_CreatesHistoryRecord()
    {
        var history = new MemberProfilesWalletHistory
        {
            WalletId = "wallet-1",
            MemberId = "member-1",
            OldStatus = WalletStatus.Pending,
            NewStatus = WalletStatus.Approved,
            ChangeReason = "Verified by admin"
        };

        history.OldStatus.Should().Be(WalletStatus.Pending);
        history.NewStatus.Should().Be(WalletStatus.Approved);
        history.ChangeReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WalletPassword_WhenWhitespaceOnly_ThrowsWalletPasswordStorageException()
    {
        var wallet = new MemberProfilesWallet { WalletType = WalletType.eWallet };

        var action = () => wallet.SetEWalletPassword("   ");

        action.Should().Throw<WalletPasswordStorageException>();
    }
}
