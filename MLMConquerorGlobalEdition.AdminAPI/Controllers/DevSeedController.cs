using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Development-only endpoint to seed the initial SuperAdmin user.
/// Disabled automatically in Production via the env check.
/// </summary>
[ApiController]
[Route("api/v1/dev")]
public class DevSeedController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole>    _roleManager;
    private readonly IWebHostEnvironment          _env;
    private readonly IDateTimeProvider            _dateTime;

    public DevSeedController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole>    roleManager,
        IWebHostEnvironment          env,
        IDateTimeProvider            dateTime)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _env         = env;
        _dateTime    = dateTime;
    }

    /// <summary>POST /api/v1/dev/seed-superadmin — creates roles + SuperAdmin user.</summary>
    [HttpPost("seed-superadmin")]
    public async Task<ActionResult<ApiResponse<SeedResultDto>>> SeedSuperAdmin(
        [FromBody] SeedRequest request,
        CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var roles = new[]
        {
            "SuperAdmin", "Admin", "CommissionManager", "BillingManager",
            "SupportManager", "SupportLevel1", "SupportLevel2", "SupportLevel3",
            "IT", "Ambassador", "Member"
        };

        foreach (var role in roles)
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Ok(ApiResponse<SeedResultDto>.Ok(new SeedResultDto("User already exists — skipped.")));

        var user = new ApplicationUser
        {
            UserName      = request.Email,
            Email         = request.Email,
            EmailConfirmed = true,
            IsActive      = true,
            CreationDate  = _dateTime.Now,
            CreatedBy     = "DevSeed"
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(ApiResponse<SeedResultDto>.Fail("SEED_FAILED", errors));
        }

        await _userManager.AddToRoleAsync(user, "SuperAdmin");

        return Ok(ApiResponse<SeedResultDto>.Ok(
            new SeedResultDto($"SuperAdmin '{request.Email}' created successfully.")));
    }

    public record SeedRequest(string Email, string Password);
    public record SeedResultDto(string Message);
}
