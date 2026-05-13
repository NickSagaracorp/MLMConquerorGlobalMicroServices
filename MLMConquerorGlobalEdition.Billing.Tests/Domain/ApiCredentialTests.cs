using FluentAssertions;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Billing.Tests.Domain;

public class ApiCredentialTests
{
    // ── ApiKeyEncrypted ────────────────────────────────────────────────────

    [Fact]
    public void ApiKeyEncrypted_WhenValueStartsWithEnc_SetsCorrectly()
    {
        var cred = new ApiCredential();
        cred.ApiKeyEncrypted = "ENC:abc123";
        cred.ApiKeyEncrypted.Should().Be("ENC:abc123");
    }

    [Fact]
    public void ApiKeyEncrypted_WhenValueIsNull_SetsToNull()
    {
        var cred = new ApiCredential { ApiKeyEncrypted = "ENC:abc" };
        cred.ApiKeyEncrypted = null;
        cred.ApiKeyEncrypted.Should().BeNull();
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("sk_live_abc123")]
    [InlineData("NOTENC:value")]
    [InlineData("")]
    public void ApiKeyEncrypted_WhenValueIsPlainText_ThrowsWalletPasswordStorageException(string plain)
    {
        var cred = new ApiCredential();
        var act = () => { cred.ApiKeyEncrypted = plain; };
        act.Should().Throw<WalletPasswordStorageException>();
    }

    // ── SecretKeyEncrypted ─────────────────────────────────────────────────

    [Fact]
    public void SecretKeyEncrypted_WhenValueStartsWithEnc_SetsCorrectly()
    {
        var cred = new ApiCredential();
        cred.SecretKeyEncrypted = "ENC:secret";
        cred.SecretKeyEncrypted.Should().Be("ENC:secret");
    }

    [Fact]
    public void SecretKeyEncrypted_WhenPlainText_ThrowsWalletPasswordStorageException()
    {
        var cred = new ApiCredential();
        var act = () => { cred.SecretKeyEncrypted = "plain-secret"; };
        act.Should().Throw<WalletPasswordStorageException>();
    }

    // ── MerchantIdEncrypted ────────────────────────────────────────────────

    [Fact]
    public void MerchantIdEncrypted_WhenValueStartsWithEnc_SetsCorrectly()
    {
        var cred = new ApiCredential();
        cred.MerchantIdEncrypted = "ENC:mid";
        cred.MerchantIdEncrypted.Should().Be("ENC:mid");
    }

    [Fact]
    public void MerchantIdEncrypted_WhenPlainText_ThrowsWalletPasswordStorageException()
    {
        var cred = new ApiCredential();
        var act = () => { cred.MerchantIdEncrypted = "merchant123"; };
        act.Should().Throw<WalletPasswordStorageException>();
    }

    // ── AdditionalSecretEncrypted ──────────────────────────────────────────

    [Fact]
    public void AdditionalSecretEncrypted_WhenValueStartsWithEnc_SetsCorrectly()
    {
        var cred = new ApiCredential();
        cred.AdditionalSecretEncrypted = "ENC:extra";
        cred.AdditionalSecretEncrypted.Should().Be("ENC:extra");
    }

    [Fact]
    public void AdditionalSecretEncrypted_WhenPlainText_ThrowsWalletPasswordStorageException()
    {
        var cred = new ApiCredential();
        var act = () => { cred.AdditionalSecretEncrypted = "extra-secret"; };
        act.Should().Throw<WalletPasswordStorageException>();
    }

    // ── IsActive default ───────────────────────────────────────────────────

    [Fact]
    public void IsActive_DefaultsToTrue()
    {
        var cred = new ApiCredential();
        cred.IsActive.Should().BeTrue();
    }
}
