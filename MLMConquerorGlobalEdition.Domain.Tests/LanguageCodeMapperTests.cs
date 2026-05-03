using FluentAssertions;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Domain.Tests;

public class LanguageCodeMapperTests
{
    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("es", "es-ES")]
    [InlineData("pt", "pt-BR")]
    [InlineData("fr", "fr-FR")]
    [InlineData("de", "de-DE")]
    [InlineData("zh", "zh-CN")]
    [InlineData("it", "it-IT")]
    [InlineData("kr", "ko-KR")] // Nick uses "kr" instead of strict ISO "ko"
    [InlineData("ge", "ka-GE")] // Nick uses "ge" instead of strict ISO "ka"
    public void ToCultureName_WithSupportedAppCode_ReturnsExpectedCultureName(string appCode, string expected)
    {
        LanguageCodeMapper.ToCultureName(appCode).Should().Be(expected);
    }

    [Theory]
    [InlineData("EN", "en-US")]
    [InlineData("KR", "ko-KR")]
    public void ToCultureName_IsCaseInsensitive(string appCode, string expected)
    {
        LanguageCodeMapper.ToCultureName(appCode).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    [InlineData("klingon")]
    public void ToCultureName_WithUnknownOrEmpty_FallsBackToDefault(string? appCode)
    {
        LanguageCodeMapper.ToCultureName(appCode).Should().Be(LanguageCodeMapper.DefaultCultureName);
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("ko-KR", "kr")]
    [InlineData("ka-GE", "ge")]
    [InlineData("zh-CN", "zh")]
    public void ToAppCode_WithSupportedCultureName_ReturnsExpectedAppCode(string cultureName, string expected)
    {
        LanguageCodeMapper.ToAppCode(cultureName).Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "en")]    // neutral parent
    [InlineData("ko", "kr")]    // neutral parent maps via prefix
    [InlineData("ka", "ge")]    // neutral parent maps via prefix
    public void ToAppCode_WithNeutralCulture_ResolvesToNearestAppCode(string neutral, string expected)
    {
        LanguageCodeMapper.ToAppCode(neutral).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx-XX")]
    public void ToAppCode_WithUnknownOrEmpty_FallsBackToDefault(string? cultureName)
    {
        LanguageCodeMapper.ToAppCode(cultureName).Should().Be(LanguageCodeMapper.DefaultAppCode);
    }

    [Fact]
    public void SupportedAppCodes_ContainsAllNineCodes()
    {
        LanguageCodeMapper.SupportedAppCodes.Should()
            .HaveCount(9)
            .And.Contain(new[] { "en", "es", "pt", "fr", "de", "zh", "it", "kr", "ge" });
    }

    [Fact]
    public void SupportedCultureNames_ContainsAllNineMappedCultures()
    {
        LanguageCodeMapper.SupportedCultureNames.Should()
            .HaveCount(9)
            .And.Contain(new[] { "en-US", "es-ES", "pt-BR", "fr-FR", "de-DE", "zh-CN", "it-IT", "ko-KR", "ka-GE" });
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("kr", true)]
    [InlineData("ko", false)]   // ko is the CultureInfo name, not the app code — must be false
    [InlineData("xx", false)]
    [InlineData(null, false)]
    public void IsSupportedAppCode_ReturnsExpected(string? appCode, bool expected)
    {
        LanguageCodeMapper.IsSupportedAppCode(appCode).Should().Be(expected);
    }
}
