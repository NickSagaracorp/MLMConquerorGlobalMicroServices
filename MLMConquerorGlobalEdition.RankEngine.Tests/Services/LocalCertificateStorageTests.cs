using MLMConquerorGlobalEdition.RankEngine.Services;

namespace MLMConquerorGlobalEdition.RankEngine.Tests.Services;

public class LocalCertificateStorageTests : IDisposable
{
    private readonly string _tempFolder =
        Path.Combine(Path.GetTempPath(), "certtest-" + Guid.NewGuid().ToString("N"));

    private LocalCertificateStorage Build() =>
        new(_tempFolder, "https://localhost:7009");

    [Fact]
    public async Task SaveAsync_WritesFileAndReturnsPublicUrl()
    {
        var storage = Build();
        var bytes   = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        var url = await storage.SaveAsync("cert-1.pdf", bytes, CancellationToken.None);

        url.Should().Be("https://localhost:7009/certificates/cert-1.pdf");
        File.Exists(Path.Combine(_tempFolder, "cert-1.pdf")).Should().BeTrue();
        (await File.ReadAllBytesAsync(Path.Combine(_tempFolder, "cert-1.pdf")))
            .Should().Equal(bytes);
    }

    [Fact]
    public async Task SaveAsync_CalledTwice_OverwritesFile()
    {
        var storage = Build();
        await storage.SaveAsync("cert-1.pdf", new byte[] { 1 }, CancellationToken.None);
        await storage.SaveAsync("cert-1.pdf", new byte[] { 2, 2 }, CancellationToken.None);

        (await File.ReadAllBytesAsync(Path.Combine(_tempFolder, "cert-1.pdf")))
            .Should().Equal(new byte[] { 2, 2 });
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingFile()
    {
        var storage = Build();
        await storage.SaveAsync("cert-1.pdf", new byte[] { 1 }, CancellationToken.None);

        await storage.DeleteAsync("cert-1.pdf", CancellationToken.None);

        File.Exists(Path.Combine(_tempFolder, "cert-1.pdf")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenFileMissing_DoesNotThrow()
    {
        var storage = Build();
        var act = async () => await storage.DeleteAsync("ghost.pdf", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAsync_StripsPathTraversalFromFileName()
    {
        var storage = Build();
        await storage.SaveAsync("../escape.pdf", new byte[] { 1 }, CancellationToken.None);

        File.Exists(Path.Combine(_tempFolder, "escape.pdf")).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, recursive: true);
    }
}
