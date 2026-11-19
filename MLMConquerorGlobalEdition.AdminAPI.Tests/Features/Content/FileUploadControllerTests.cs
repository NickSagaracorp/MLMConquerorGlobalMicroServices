using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.SharedKernel;
using Moq;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Content;

/// <summary>
/// La subida de imágenes de producto valida el contenido por su firma (los primeros bytes de
/// cada formato) y exige que coincida con la extensión. Ya no usa ImageSharp: la 4.x pide licencia
/// comercial para compilar en Release y solo servía para validar y hacer una miniatura que nadie
/// leía.
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

    // Cabeceras reales de cada formato seguidas de relleno: la validación solo mira la firma.
    private static byte[] Con(byte[] cabecera) => cabecera.Concat(new byte[64]).ToArray();
    private static readonly byte[] Png  = Con([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    private static readonly byte[] Jpeg = Con([0xFF, 0xD8, 0xFF, 0xE0]);
    private static readonly byte[] Gif  = Con("GIF89a"u8.ToArray());
    private static readonly byte[] Webp = Con([.. "RIFF"u8.ToArray(), 0x24, 0, 0, 0, .. "WEBPVP8 "u8.ToArray()]);

    public static TheoryData<byte[], string> ImagenesValidas => new()
    {
        { Png,  "foto.png"  },
        { Jpeg, "foto.jpg"  },
        { Jpeg, "foto.JPEG" },
        { Gif,  "foto.gif"  },
        { Webp, "foto.webp" },
    };

    [Theory]
    [MemberData(nameof(ImagenesValidas))]
    public async Task Una_imagen_valida_se_guarda_tal_cual_con_nombre_nuevo(byte[] contenido, string nombre)
    {
        var resultado = await Controller().UploadFile(Archivo(contenido, nombre));

        var ok = resultado.Should().BeOfType<OkObjectResult>().Subject;
        var cuerpo = ok.Value.Should().BeOfType<ApiResponse<FileUploadController.UploadResult>>().Subject;
        var extension = Path.GetExtension(nombre).ToLowerInvariant();
        cuerpo.Data!.Url.Should().StartWith("https://admin.example/uploads/").And.EndWith(extension);

        var guardado = Directory.GetFiles(Path.Combine(_webRoot, "uploads")).Should().ContainSingle().Subject;
        Path.GetFileName(guardado).Should().NotBe(nombre);
        (await File.ReadAllBytesAsync(guardado)).Should().Equal(contenido);
    }

    [Fact]
    public async Task Un_archivo_que_no_es_imagen_se_rechaza_aunque_tenga_extension_de_imagen()
    {
        var resultado = await Controller().UploadFile(Archivo("<?php echo 1; ?>"u8.ToArray(), "foto.png"));

        Rechazo(resultado).Should().Be("INVALID_IMAGE_CONTENT");
        Directory.Exists(Path.Combine(_webRoot, "uploads")).Should().BeFalse();
    }

    [Fact]
    public async Task Una_imagen_cuya_firma_no_coincide_con_la_extension_se_rechaza()
    {
        // Un GIF disfrazado de PNG: el navegador lo serviría con el tipo equivocado.
        var resultado = await Controller().UploadFile(Archivo(Gif, "foto.png"));

        Rechazo(resultado).Should().Be("INVALID_IMAGE_CONTENT");
    }

    [Fact]
    public async Task Un_riff_que_no_es_webp_se_rechaza()
    {
        var wav = Con([.. "RIFF"u8.ToArray(), 0x24, 0, 0, 0, .. "WAVEfmt "u8.ToArray()]);

        var resultado = await Controller().UploadFile(Archivo(wav, "foto.webp"));

        Rechazo(resultado).Should().Be("INVALID_IMAGE_CONTENT");
    }

    [Fact]
    public async Task Una_extension_no_permitida_se_rechaza()
    {
        var resultado = await Controller().UploadFile(Archivo(Png, "foto.svg"));

        Rechazo(resultado).Should().Be("INVALID_FILE_TYPE");
    }

    private static string? Rechazo(IActionResult resultado) =>
        resultado.Should().BeOfType<BadRequestObjectResult>().Which.Value
            .Should().BeOfType<ApiResponse<FileUploadController.UploadResult>>().Which.ErrorCode;
}
