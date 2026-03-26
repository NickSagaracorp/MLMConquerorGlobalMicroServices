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
    private readonly IWebHostEnvironment _env;
    public FileUploadController(IWebHostEnvironment env) => _env = env;

    [HttpPost]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_FILE", "No file provided."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowed.Contains(ext))
            return BadRequest(ApiResponse<UploadResult>.Fail("INVALID_FILE_TYPE", "Only image files are allowed."));

        var uploadsPath = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        Directory.CreateDirectory(uploadsPath);

        var baseName = Guid.NewGuid().ToString();
        var fileName = $"{baseName}{ext}";
        var thumbName = $"{baseName}_thumb.webp";

        var filePath = Path.Combine(uploadsPath, fileName);
        var thumbPath = Path.Combine(uploadsPath, thumbName);

        // Save original
        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream, ct);
        }

        // Generate 100x100 thumbnail
        using (var img = await Image.LoadAsync(filePath, ct))
        {
            img.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(100, 100),
                Mode = ResizeMode.Crop
            }));
            await img.SaveAsWebpAsync(thumbPath, ct);
        }

        var req = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";
        var url = $"{baseUrl}/uploads/{fileName}";
        var thumbUrl = $"{baseUrl}/uploads/{thumbName}";

        return Ok(ApiResponse<UploadResult>.Ok(new UploadResult(url, thumbUrl), "File uploaded successfully."));
    }

    public record UploadResult(string Url, string ThumbUrl);
}
