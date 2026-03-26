using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.CreateProductCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.DeleteProductCommission;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.GetProductCommissions;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductCommissions.UpdateProductCommission;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/product-commissions")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class ProductCommissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductCommissionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetProductCommissionsQuery(productId, new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<ProductCommissionDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<ProductCommissionDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductCommissionRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateProductCommissionCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value!, "Product commission created."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateProductCommissionRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateProductCommissionCommand(id, request), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Product commission updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteProductCommissionCommand(id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Product commission deleted."));
    }
}
