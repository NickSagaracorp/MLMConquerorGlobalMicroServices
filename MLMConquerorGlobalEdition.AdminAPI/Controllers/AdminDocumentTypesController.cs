using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.CreateDocumentType;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.DeleteDocumentType;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetDocumentTypes;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpdateDocumentType;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/document-types")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminDocumentTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminDocumentTypesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDocumentTypesQuery(), ct);
        return Ok(ApiResponse<List<DocumentTypeDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentTypeRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateDocumentTypeCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<DocumentTypeDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<DocumentTypeDto>.Ok(result.Value!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDocumentTypeRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateDocumentTypeCommand(id, request), ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound(ApiResponse<DocumentTypeDto>.Fail(result.ErrorCode, result.Error!))
                : BadRequest(ApiResponse<DocumentTypeDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<DocumentTypeDto>.Ok(result.Value!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteDocumentTypeCommand(id), ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "NOT_FOUND"
                ? NotFound(ApiResponse<bool>.Fail(result.ErrorCode, result.Error!))
                : BadRequest(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<bool>.Ok(true));
    }
}
