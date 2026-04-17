using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.TicketManagementSystem.DTOs;
using MLMConquerorGlobalEdition.TicketManagementSystem.Features.KnowledgeBase;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Controllers;

[ApiController]
[Route("api/v1/kb")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IMediator _mediator;

    public KnowledgeBaseController(IMediator mediator) => _mediator = mediator;

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchKnowledgeBaseQuery(q, page, pageSize), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<PagedResult<KbArticleSummaryDto>>.Ok(result.Value!));
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] string titlePartial, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetKbSuggestionsQuery(titlePartial), ct);
        if (!result.IsSuccess) return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<IEnumerable<KbArticleSummaryDto>>.Ok(result.Value!));
    }

    [HttpPost("articles")]
    public async Task<IActionResult> Create([FromBody] CreateKbArticleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateKbArticleCommand(request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<KbArticleDto>.Ok(result.Value!));
    }

    [HttpGet("articles/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetKbArticleBySlugQuery(slug), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<KbArticleDto>.Ok(result.Value!));
    }

    [HttpPut("articles/{articleId}")]
    public async Task<IActionResult> Update(string articleId, [FromBody] CreateKbArticleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateKbArticleCommand(articleId, request), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("articles/{articleId}/publish")]
    public async Task<IActionResult> Publish(string articleId, CancellationToken ct)
    {
        var result = await _mediator.Send(new PublishKbArticleCommand(articleId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpDelete("articles/{articleId}")]
    public async Task<IActionResult> Delete(string articleId, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteKbArticleCommand(articleId), ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "FORBIDDEN") return StatusCode(403, ApiResponse<object>.Fail(result.ErrorCode, result.Error!, HttpContext.TraceIdentifier));
            return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<bool>.Ok(true));
    }

    [HttpPost("articles/{articleId}/feedback")]
    public async Task<IActionResult> SubmitFeedback(string articleId, [FromBody] KbFeedbackRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitKbFeedbackCommand(articleId, request), ct);
        if (!result.IsSuccess) return NotFound(ApiResponse<object>.Fail(result.ErrorCode!, result.Error!, HttpContext.TraceIdentifier));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
