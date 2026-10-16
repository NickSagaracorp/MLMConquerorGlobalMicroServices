using MLMConquerorGlobalEdition.Repository.Services.Entrax;
using MLMConquerorGlobalEdition.SignupAPI.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>Lo que Entrax manda para pintar se valida antes de llegar a un img src o a un href.</summary>
public class EntraxQrSanitizerTests
{
    [Fact]
    public void Una_imagen_PNG_en_base64_pasa()
        => EntraxQrSanitizer.SafeImage(FakeEntraxClient.PlaceholderPng).Should().Be(FakeEntraxClient.PlaceholderPng);

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("data:image/png;base64,")]
    [InlineData("data:image/png;base64,no es base64")]
    [InlineData("https://evil.example.com/qr.png")]
    [InlineData(null)]
    public void Cualquier_otra_cosa_no_se_pinta(string? valor)
        => EntraxQrSanitizer.SafeImage(valor).Should().BeNull();

    [Fact]
    public void Un_enlace_https_pasa_tal_cual()
        => EntraxQrSanitizer.SafeLink("https://qr.nspk.ru/AS100?x=1").Should().Be("https://qr.nspk.ru/AS100?x=1");

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://qr.nspk.ru/AS100")]
    [InlineData("/relativo")]
    [InlineData(null)]
    public void Un_enlace_que_no_es_https_se_descarta(string? valor)
        => EntraxQrSanitizer.SafeLink(valor).Should().BeNull();
}
