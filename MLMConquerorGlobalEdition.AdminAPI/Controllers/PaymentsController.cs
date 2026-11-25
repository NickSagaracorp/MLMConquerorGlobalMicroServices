using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPayments;
using MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPaymentsGrid;
using MLMConquerorGlobalEdition.Repository.Grid;
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

    /// <summary>
    /// Lectura del grid de servidor de Payment History en AdminWeb. El CustomAdaptor de Syncfusion manda un
    /// <see cref="GridDataRequest"/> (pagina, orden, filtros de columna, busqueda) para que filtrar y ordenar abarquen
    /// TODOS los pagos y no solo la pagina cargada. Estado, miembro y rango de fechas son los controles de la cabecera
    /// y viajan en la query. El GET de arriba se queda para quien ya lo usa.
    /// </summary>
    [HttpPost("grid")]
    public async Task<IActionResult> GetPaymentsGrid(
        [FromBody] GridDataRequest request,
        [FromQuery] string? status = null,
        [FromQuery] string? memberId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAdminPaymentsGridQuery(request, status, memberId, from, to), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminPaymentDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminPaymentDto>>.Ok(result.Value!));
    }
}
