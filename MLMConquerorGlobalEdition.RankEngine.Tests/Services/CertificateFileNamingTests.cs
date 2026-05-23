using MLMConquerorGlobalEdition.RankEngine.Services;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class CertificateFileNamingTests
{
    [Fact]
    public void Build_ProducesHashUnderscoreMemberUnderscoreRankSlug()
    {
        var name = CertificateFileNaming.Build(
            memberGuidId: "11111111-1111-1111-1111-111111111111",
            memberId: "AMB-000123",
            rankName: "Silver");

        name.Should().MatchRegex("^[0-9a-f]{64}_AMB-000123_Silver\\.pdf$");
    }

    [Fact]
    public void Build_IsDeterministic_SameInputsProduceSameName()
    {
        var a = CertificateFileNaming.Build("guid-x", "AMB-000999", "Gold");
        var b = CertificateFileNaming.Build("guid-x", "AMB-000999", "Gold");
        a.Should().Be(b);
    }

    [Fact]
    public void Build_DifferentGuid_ProducesDifferentName()
    {
        var a = CertificateFileNaming.Build("guid-a", "AMB-000999", "Gold");
        var b = CertificateFileNaming.Build("guid-b", "AMB-000999", "Gold");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Build_StripsWhitespaceFromRankName()
    {
        var name = CertificateFileNaming.Build("guid-x", "AMB-000123", "Double Diamond");
        name.Should().EndWith("_AMB-000123_DoubleDiamond.pdf");
    }
}
