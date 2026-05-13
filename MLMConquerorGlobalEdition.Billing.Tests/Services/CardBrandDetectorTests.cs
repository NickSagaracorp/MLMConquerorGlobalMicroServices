using FluentAssertions;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services;

public class CardBrandDetectorTests
{
    private readonly CardBrandDetector _detector = new();

    [Theory]
    [InlineData("411111", CardBrand.Visa)]
    [InlineData("400000", CardBrand.Visa)]
    [InlineData("499999", CardBrand.Visa)]
    public void Detect_VisaBins_ReturnsVisa(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);

    [Theory]
    [InlineData("510000", CardBrand.MasterCard)]
    [InlineData("559999", CardBrand.MasterCard)]
    [InlineData("540000", CardBrand.MasterCard)]
    [InlineData("222100", CardBrand.MasterCard)]
    [InlineData("272099", CardBrand.MasterCard)]
    public void Detect_MasterCardBins_ReturnsMasterCard(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);

    [Theory]
    [InlineData("340000", CardBrand.Amex)]
    [InlineData("370000", CardBrand.Amex)]
    public void Detect_AmexBins_ReturnsAmex(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);

    [Theory]
    [InlineData("352800", CardBrand.Jcb)]
    [InlineData("358999", CardBrand.Jcb)]
    [InlineData("356000", CardBrand.Jcb)]
    public void Detect_JcbBins_ReturnsJcb(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);

    [Theory]
    [InlineData("630400", CardBrand.Maestro)]
    [InlineData("675900", CardBrand.Maestro)]
    public void Detect_MaestroBins_ReturnsMaestro(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);

    [Theory]
    [InlineData("670300", CardBrand.Bancontact)]
    [InlineData("670399", CardBrand.Bancontact)]
    public void Detect_BancontactBins_ReturnsBancontact(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);

    [Theory]
    [InlineData("", CardBrand.Other)]
    [InlineData("   ", CardBrand.Other)]
    [InlineData("ABCDEF", CardBrand.Other)]
    [InlineData("999999", CardBrand.Other)]
    public void Detect_UnknownOrInvalidBins_ReturnsOther(string bin, CardBrand expected)
        => _detector.Detect(bin).Should().Be(expected);
}
