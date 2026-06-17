using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Payout.Anchoring;
using Xunit;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Payout;

public class MerkleTreeTests
{
    [Fact]
    public void ComputeRoot_IsDeterministic_AndOrderSensitive()
    {
        var a = MerkleTree.ComputeRoot(new[] { "aa", "bb", "cc" });
        var b = MerkleTree.ComputeRoot(new[] { "aa", "bb", "cc" });
        var c = MerkleTree.ComputeRoot(new[] { "bb", "aa", "cc" });
        a.Should().Be(b);
        a.Should().NotBe(c); // order matters
        a.Length.Should().Be(64);
    }

    [Fact]
    public void ChainHash_LinksPrevAndCurrent()
    {
        var h1 = MerkleTree.ChainHash(MerkleTree.Genesis, "sha-1");
        var h2 = MerkleTree.ChainHash(h1, "sha-2");
        h1.Should().NotBe(h2);
        // recomputation reproduces the same chain
        MerkleTree.ChainHash(MerkleTree.Genesis, "sha-1").Should().Be(h1);
    }
}
