using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.RankEngine.Services;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

/// <summary>
/// Integration-style tests for the certificate PDF filler — these exercise the real
/// iText pipeline against the real template PDFs in the RankEngine project, so they
/// catch packaging issues (missing iText adapters) and template-layout regressions.
/// </summary>
public class ITextCertificatePdfFillerServiceTests
{
    /// <summary>The real CertificateTemplates folder in the sibling RankEngine project.</summary>
    private static string TemplatesFolder()
        => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "MLMConquerorGlobalEdition.RankEngine", "CertificateTemplates"));

    private static ITextCertificatePdfFillerService Build()
        => new(TemplatesFolder(), new Mock<ILogger<ITextCertificatePdfFillerService>>().Object);

    [Fact]
    public async Task FillAsync_WithRealSilverTemplate_ProducesValidPdf()
    {
        var data = new CertificateTemplateData(
            FullName:   "Jane Q. Ambassador",
            MemberId:   "AMB-000123",
            RankName:   "Silver",
            AchievedAt: new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc));

        var bytes = await Build().FillAsync(1, data, CancellationToken.None);

        bytes.Should().NotBeNullOrEmpty();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task FillAsync_WithRealBlackRoyalTemplateAndLongName_ProducesValidPdf()
    {
        // A long name also exercises the adaptive font-size path.
        var data = new CertificateTemplateData(
            FullName:   "Maximilian Alexander Worthington III",
            MemberId:   "AMB-000999",
            RankName:   "Black Royal",
            AchievedAt: new DateTime(2026, 5, 22, 0, 0, 0, DateTimeKind.Utc));

        var bytes = await Build().FillAsync(19, data, CancellationToken.None);

        bytes.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task FillAsync_WhenTemplateMissing_ThrowsFileNotFound()
    {
        var data = new CertificateTemplateData("X", "AMB-1", "Ghost", DateTime.UtcNow);

        var act = async () => await Build().FillAsync(999, data, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
