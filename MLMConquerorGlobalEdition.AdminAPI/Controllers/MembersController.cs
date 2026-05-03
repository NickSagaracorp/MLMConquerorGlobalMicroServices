using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMember;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.GetMembers;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3")]
public class MembersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;

    public MembersController(IMediator mediator, AppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? sponsorId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool bypassCache = false,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetMembersQuery(new PagedRequest { Page = page, PageSize = pageSize }, status, sponsorId, search, bypassCache), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<AdminMemberDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<AdminMemberDto>>.Ok(result.Value!));
    }

    [HttpGet("members/{memberId}")]
    public async Task<IActionResult> GetMember(string memberId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMemberQuery(memberId), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<AdminMemberDetailDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminMemberDetailDto>.Ok(result.Value!));
    }

    [HttpPut("members/{memberId}/status")]
    public async Task<IActionResult> UpdateMemberStatus(
        string memberId,
        [FromBody] UpdateMemberStatusRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateMemberStatusCommand(memberId, request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<AdminMemberDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<AdminMemberDto>.Ok(result.Value!));
    }

    [HttpGet("members/autocomplete")]
    public async Task<IActionResult> AutocompleteMembers(
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(ApiResponse<IEnumerable<MemberAutocompleteDto>>.Ok(Enumerable.Empty<MemberAutocompleteDto>()));

        var term = q.ToLower();
        var items = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.FirstName.ToLower().Contains(term) ||
                        m.LastName.ToLower().Contains(term) ||
                        m.MemberId.ToLower().Contains(term))
            .OrderBy(m => m.FirstName)
            .Take(15)
            .Select(m => new MemberAutocompleteDto(m.MemberId, m.FirstName + " " + m.LastName, m.Country ?? string.Empty))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<MemberAutocompleteDto>>.Ok(items));
    }

    private record MemberAutocompleteDto(string MemberId, string FullName, string Country);
}
