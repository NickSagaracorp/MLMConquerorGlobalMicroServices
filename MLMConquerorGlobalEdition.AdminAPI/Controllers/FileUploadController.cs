using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Subida de imágenes de producto desde el admin (AdminProducts). El archivo se guarda tal como
/// llega, con un nombre nuevo, después de comprobar que su contenido es de verdad una imagen del
/// formato que dice su extensión.
/// </summary>
[ApiController]
[Route("api/v1/admin/upload")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class FileUploadController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    // Max file size: 5 MB (enforced server-side)
    private const long MaxFileSizeBytes = 5_000_000;

    // Lo justo para reconocer cualquiera de las firmas (WebP es la más larga: 12 bytes).
    private const int SignatureLength = 12;

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileUploadController> _logger;

    public FileUploadController(IWebHostEnvironment env, ILogger<FileUploadController> logger)
    {
        _env    = env;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(5_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_000_000)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_FILE", "No file provided."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<UploadResult>.Fail("FILE_TOO_LARGE",
                $"Maximum allowed file size is {MaxFileSizeBytes / 1_000_000} MB."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_FILE_TYPE",
                "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp)."));

        // El contenido tiene que empezar con la firma del formato de su extensión. Un archivo con
        // otro contenido (un script, un HTML, otro formato) se rechaza antes de tocar el disco.
        var header = new byte[SignatureLength];
        int read;
        await using (var probe = file.OpenReadStream())
            read = await probe.ReadAtLeastAsync(header, SignatureLength, throwOnEndOfStream: false, ct);

        if (!MatchesSignature(ext, header.AsSpan(0, read)))
        {
            _logger.LogWarning(
                "File upload rejected: content does not match the {Extension} signature. " +
                "FileName={FileName}, ContentType={ContentType}",
                ext, file.FileName, file.ContentType);

            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_IMAGE_CONTENT",
                "The uploaded file is not a valid image."));
        }

        var uploadsPath = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads");
        Directory.CreateDirectory(uploadsPath);

        // Use UUID filename — original name is never preserved (prevents path traversal)
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        try
        {
            await using var stream = System.IO.File.Create(filePath);
            await file.CopyToAsync(stream, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write uploaded file to disk.");
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
            return StatusCode(500, ApiResponse<UploadResult>.Fail("UPLOAD_FAILED",
                "An error occurred while saving the file."));
        }

        var req     = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";

        return Ok(ApiResponse<UploadResult>.Ok(
            new UploadResult($"{baseUrl}/uploads/{fileName}"),
            "File uploaded successfully."));
    }

    /// <summary>Firma (magic bytes) de cada formato permitido.</summary>
    private static bool MatchesSignature(string ext, ReadOnlySpan<byte> h) => ext switch
    {
        ".png"           => h.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ".jpg" or ".jpeg" => h.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
        ".gif"           => h.StartsWith("GIF87a"u8) || h.StartsWith("GIF89a"u8),
        ".webp"          => h.Length >= 12 && h.StartsWith("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8),
        _                => false,
    };

    public record UploadResult(string Url);
}
