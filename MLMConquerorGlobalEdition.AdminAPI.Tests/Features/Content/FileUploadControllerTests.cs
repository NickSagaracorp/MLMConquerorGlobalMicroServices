using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.SharedKernel;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Content;

/// <summary>
/// La subida valida el contenido con ImageSharp y genera la miniatura webp. Fija ese
/// comportamiento al pasar de ImageSharp 3.1 a 4.1 (GHSA-gwg2-r3hj-4w44).
/// </summary>
public class FileUploadControllerTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "upload-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }

    private FileUploadController Controller()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.WebRootPath).Returns(_webRoot);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("admin.example");
        return new FileUploadController(env.Object, NullLogger<FileUploadController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static IFormFile Archivo(byte[] contenido, string nombre) =>
        new FormFile(new MemoryStream(contenido), 0, contenido.Length, "file", nombre)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

    private static byte[] Png(int ancho, int alto)
    {
        using var imagen = new Image<Rgba32>(ancho, alto, new Rgba32(200, 10, 10));
        using var ms = new MemoryStream();
        imagen.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Una_imagen_valida_se_guarda_con_su_miniatura_de_100x100()
    {
        var resultado = await Controller().UploadFile(Archivo(Png(300, 200), "foto.png"));

        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        var cuerpo = ok.Value.Should().BeOfType<ApiResponse<FileUploadController.UploadResult>>().Subject;
        cuerpo.Data!.Url.Should().StartWith("https://admin.example/uploads/").And.EndWith(".png");

        var uploads = Path.Combine(_webRoot, "uploads");
        var miniatura = Directory.GetFiles(uploads, "*_thumb.webp").Should().ContainSingle().Subject;
        using var thumb = await Image.LoadAsync(miniatura);
        thumb.Width.Should().Be(100);
        thumb.Height.Should().Be(100);
    }

    [Fact]
    public async Task Un_archivo_que_no_es_imagen_se_rechaza_aunque_tenga_extension_de_imagen()
    {
        var resultado = await Controller().UploadFile(Archivo("<?php echo 1; ?>"u8.ToArray(), "foto.png"));

        var bad = resultado.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value.Should().BeOfType<ApiResponse<FileUploadController.UploadResult>>()
            .Which.ErrorCode.Should().Be("INVALID_IMAGE_CONTENT");
        Directory.Exists(Path.Combine(_webRoot, "uploads")).Should().BeFalse();
    }

    [Fact]
    public async Task Una_extension_no_permitida_se_rechaza()
    {
        var resultado = await Controller().UploadFile(Archivo(Png(10, 10), "foto.svg"));

        resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeOfType<ApiResponse<FileUploadController.UploadResult>>()
            .Which.ErrorCode.Should().Be("INVALID_FILE_TYPE");
    }
}
