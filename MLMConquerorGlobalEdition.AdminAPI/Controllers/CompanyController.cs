using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;
using MLMConquerorGlobalEdition.AdminAPI.Features.Company.GetCompanyInfo;
using MLMConquerorGlobalEdition.AdminAPI.Features.Company.UpdateCompanyInfo;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/company")]
[Authorize(Roles = "SuperAdmin")]
public class CompanyController : ControllerBase
{
    private readonly ISender _mediator;

    public CompanyController(ISender mediator) => _mediator = mediator;

    /// <summary>Returns the singleton company information record.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCompanyInfoQuery(), ct);
        return result.IsSuccess
            ? Ok(ApiResponse<CompanyInfoDto>.Ok(result.Value!))
            : NotFound(ApiResponse<CompanyInfoDto>.Fail(result.ErrorCode!, result.Error!));
    }

    /// <summary>
    /// Creates or replaces the singleton company information record.
    /// Only SuperAdmin may update company branding — it affects certificates and emails.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCompanyInfoCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.IsSuccess
            ? Ok(ApiResponse<CompanyInfoDto>.Ok(result.Value!))
            : BadRequest(ApiResponse<CompanyInfoDto>.Fail(result.ErrorCode!, result.Error!));
    }
}
