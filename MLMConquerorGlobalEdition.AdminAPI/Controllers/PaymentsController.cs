using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPayments;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/payments")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? memberId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetAdminPaymentsQuery(new PagedRequest { Page = page, PageSize = pageSize }, status, memberId), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminPaymentDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminPaymentDto>>.Ok(result.Value!));
    }
}
