using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductLoyaltyPoints;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.CreateProductLoyaltyPoints;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.DeleteProductLoyaltyPoints;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.GetProductLoyaltyPoints;
using MLMConquerorGlobalEdition.AdminAPI.Features.ProductLoyaltyPoints.UpdateProductLoyaltyPoints;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/products/{productId}/loyalty-points")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class ProductLoyaltyPointsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductLoyaltyPointsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(string productId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetProductLoyaltyPointsQuery(productId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<IEnumerable<ProductLoyaltyPointsDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<IEnumerable<ProductLoyaltyPointsDto>>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string productId,
        [FromBody] CreateProductLoyaltyPointsRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateProductLoyaltyPointsCommand(productId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<int>.Ok(result.Value!, "Product loyalty points setting created."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        string productId,
        int id,
        [FromBody] UpdateProductLoyaltyPointsRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateProductLoyaltyPointsCommand(productId, id, request), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Product loyalty points setting updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(string productId, int id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteProductLoyaltyPointsCommand(productId, id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Product loyalty points setting deactivated."));
    }
}
