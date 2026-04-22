using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetS3Config;
using MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpsertS3Config;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/s3-config")]
[Authorize(Roles = "SuperAdmin")]
public class AdminS3ConfigController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminS3ConfigController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetS3ConfigQuery(), ct);
        return Ok(ApiResponse<S3StorageConfigDto>.Ok(result.Value!));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] S3StorageConfigDto request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpsertS3ConfigCommand(request), ct);
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<S3StorageConfigDto>.Fail(result.ErrorCode!, result.Error!));
        return Ok(ApiResponse<S3StorageConfigDto>.Ok(result.Value!));
    }
}
