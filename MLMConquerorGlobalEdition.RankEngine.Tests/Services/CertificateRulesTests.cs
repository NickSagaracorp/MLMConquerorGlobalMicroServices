using MLMConquerorGlobalEdition.RankEngine.Services;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class CertificateRulesTests
{
    [Fact]
    public void IsCertificateEligible_SortOrderZero_ReturnsFalse()
        => CertificateRules.IsCertificateEligible(0).Should().BeFalse();   // Lifestyle Consultant

    [Fact]
    public void IsCertificateEligible_SortOrderOne_ReturnsTrue()
        => CertificateRules.IsCertificateEligible(1).Should().BeTrue();    // Silver

    [Fact]
    public void IsCertificateEligible_SortOrderNineteen_ReturnsTrue()
        => CertificateRules.IsCertificateEligible(19).Should().BeTrue();   // Black Royal
}
