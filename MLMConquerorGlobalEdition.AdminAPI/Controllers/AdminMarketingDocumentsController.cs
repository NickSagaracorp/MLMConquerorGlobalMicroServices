using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.DeleteMarketingDocument;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetMarketingDocuments;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpdateMarketingDocument;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UploadMarketingDocument;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/marketing-documents")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMarketingDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminMarketingDocumentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int?  documentTypeId = null,
        [FromQuery] bool? isActive       = null,
        CancellationToken ct             = default)
    {
        var result = await _mediator.Send(new GetMarketingDocumentsQuery(documentTypeId, isActive), ct);
        return Ok(ApiResponse<List<MarketingDocumentDto>>.Ok(result.Value!));
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<IActionResult> Upload(
        [FromForm] string title,
        [FromForm] int    documentTypeId,
        [FromForm] string languageCode,
        [FromForm] string languageName,
        IFormFile         file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<MarketingDocumentDto>.Fail("INVALID_FILE", "A file is required."));

        await using var stream = file.OpenReadStream();

        var result = await _mediator.Send(new UploadMarketingDocumentCommand(
            title,
            documentTypeId,
            languageCode,
            languageName,
            stream,
            file.FileName,
            file.Length,
            file.ContentType
        ), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<MarketingDocumentDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<MarketingDocumentDto>.Ok(result.Value!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMarketingDocumentRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateMarketingDocumentCommand(id, request), ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound(ApiResponse<MarketingDocumentDto>.Fail(result.ErrorCode, result.Error!))
                : BadRequest(ApiResponse<MarketingDocumentDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<MarketingDocumentDto>.Ok(result.Value!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteMarketingDocumentCommand(id), ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound(ApiResponse<bool>.Fail(result.ErrorCode, result.Error!))
                : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
