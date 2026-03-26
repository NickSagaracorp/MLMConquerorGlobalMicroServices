using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/system-users")]
[Authorize(Roles = "SuperAdmin")]
public class SystemUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDateTimeProvider _dateTime;

    public SystemUsersController(UserManager<ApplicationUser> userManager, IDateTimeProvider dateTime)
    {
        _userManager = userManager;
        _dateTime = dateTime;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _userManager.Users
            .Where(u => u.MemberProfileId == null)
            .OrderByDescending(u => u.CreationDate);

        var total = await query.CountAsync(ct);
        var users = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var dtos = new List<SystemUserDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            dtos.Add(new SystemUserDto(
                u.Id,
                u.UserName ?? u.Email ?? string.Empty,
                u.Email ?? string.Empty,
                roles.FirstOrDefault() ?? string.Empty,
                u.IsActive,
                u.LastLoginAt,
                u.CreationDate));
        }

        var result = new PagedResult<SystemUserDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResult<SystemUserDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSystemUserRequest dto, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing is not null)
            return BadRequest(ApiResponse<string>.Fail("EMAIL_TAKEN", "A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            IsActive = dto.IsActive,
            CreationDate = _dateTime.Now,
            CreatedBy = User.Identity?.Name ?? "admin"
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(ApiResponse<string>.Fail("CREATE_FAILED",
                string.Join(", ", result.Errors.Select(e => e.Description))));

        if (!string.IsNullOrWhiteSpace(dto.Role))
            await _userManager.AddToRoleAsync(user, dto.Role);

        return Ok(ApiResponse<string>.Ok(user.Id, "System user created."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateSystemUserRequest dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "User not found."));

        user.Email = dto.Email;
        user.UserName = dto.Email;
        user.IsActive = dto.IsActive;
        await _userManager.UpdateAsync(user);

        var existingRoles = await _userManager.GetRolesAsync(user);
        if (existingRoles.Any())
            await _userManager.RemoveFromRolesAsync(user, existingRoles);

        if (!string.IsNullOrWhiteSpace(dto.Role))
            await _userManager.AddToRoleAsync(user, dto.Role);

        return Ok(ApiResponse<bool>.Ok(true, "Updated."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound(ApiResponse<bool>.Fail("NOT_FOUND", "User not found."));

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        return Ok(ApiResponse<bool>.Ok(true, "Deactivated."));
    }

    private record SystemUserDto(string Id, string UserName, string Email, string Role,
        bool IsActive, DateTime? LastLoginAt, DateTime CreationDate);

    public record CreateSystemUserRequest(string Email, string Password, string Role, bool IsActive);
    public record UpdateSystemUserRequest(string Email, string Role, bool IsActive);
}
