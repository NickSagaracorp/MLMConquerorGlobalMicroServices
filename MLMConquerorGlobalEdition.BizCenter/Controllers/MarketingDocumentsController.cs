using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Marketing;
using MLMConquerorGlobalEdition.BizCenter.Features.Marketing.GetMarketingDocuments;
using MLMConquerorGlobalEdition.BizCenter.Features.Marketing.GetPresignedDownloadUrl;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Controllers;

[ApiController]
[Route("api/v1/bizcenter/marketing-documents")]
[Authorize]
public class MarketingDocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketingDocumentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? documentTypeId = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMarketingDocumentsQuery(documentTypeId), ct);
        return Ok(ApiResponse<List<MarketingDocumentSummaryDto>>.Ok(result.Value!));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> GetDownloadUrl(int id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPresignedDownloadUrlQuery(id), ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound(ApiResponse<string>.Fail(result.ErrorCode, result.Error!))
                : BadRequest(ApiResponse<string>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<string>.Ok(result.Value!));
    }
}
