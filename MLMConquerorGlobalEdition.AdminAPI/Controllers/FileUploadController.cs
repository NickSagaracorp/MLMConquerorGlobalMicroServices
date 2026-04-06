using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/upload")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class FileUploadController : ControllerBase
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    // Max file size: 5 MB (enforced server-side)
    private const long MaxFileSizeBytes = 5_000_000;

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
        // ── Basic presence check ─────────────────────────────────────────────
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_FILE", "No file provided."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<UploadResult>.Fail("FILE_TOO_LARGE",
                $"Maximum allowed file size is {MaxFileSizeBytes / 1_000_000} MB."));

        // ── Extension whitelist check (first gate) ────────────────────────────
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_FILE_TYPE",
                "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp)."));

        // ── Magic bytes validation via ImageSharp (real content check) ────────
        // Load the image from the stream BEFORE saving to disk.
        // If the file is not a valid image format (regardless of extension),
        // ImageSharp will throw an UnknownImageFormatException.
        Image? validatedImage = null;
        try
        {
            file.OpenReadStream().Position = 0;
            validatedImage = await Image.LoadAsync(file.OpenReadStream(), ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            _logger.LogWarning(
                "File upload rejected: content does not match a known image format. " +
                "FileName={FileName}, ContentType={ContentType}",
                file.FileName, file.ContentType);

            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_IMAGE_CONTENT",
                "The uploaded file is not a valid image."));
        }

        // ── Prepare storage ───────────────────────────────────────────────────
        var uploadsPath = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads");
        Directory.CreateDirectory(uploadsPath);

        // Use UUID filename — original name is never preserved (prevents path traversal)
        var baseName  = Guid.NewGuid().ToString("N");
        var fileName  = $"{baseName}{ext}";
        var thumbName = $"{baseName}_thumb.webp";
        var filePath  = Path.Combine(uploadsPath, fileName);
        var thumbPath = Path.Combine(uploadsPath, thumbName);

        // ── Persist original (already validated in memory) ────────────────────
        try
        {
            file.OpenReadStream().Position = 0;
            await using var stream = System.IO.File.Create(filePath);
            await file.CopyToAsync(stream, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write uploaded file to disk.");
            return StatusCode(500, ApiResponse<UploadResult>.Fail("UPLOAD_FAILED",
                "An error occurred while saving the file."));
        }

        // ── Generate 100×100 thumbnail from in-memory image ───────────────────
        try
        {
            using (validatedImage)
            {
                validatedImage.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(100, 100),
                    Mode = ResizeMode.Crop
                }));
                await validatedImage.SaveAsWebpAsync(thumbPath, ct);
            }
        }
        catch (Exception ex)
        {
            // Delete the original if thumbnail generation fails
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _logger.LogError(ex, "Thumbnail generation failed. Original file cleaned up.");
            return StatusCode(500, ApiResponse<UploadResult>.Fail("THUMBNAIL_FAILED",
                "An error occurred while processing the image."));
        }

        var req     = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";

        return Ok(ApiResponse<UploadResult>.Ok(
            new UploadResult($"{baseUrl}/uploads/{fileName}", $"{baseUrl}/uploads/{thumbName}"),
            "File uploaded successfully."));
    }

    public record UploadResult(string Url, string ThumbUrl);
}
