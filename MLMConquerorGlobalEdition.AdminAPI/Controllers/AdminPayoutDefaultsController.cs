using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// CRUD for the per-country default payout gateway used by the signup
/// pipeline to seed each new ambassador's preferred wallet. Admin-only;
/// the lookup at signup is best-effort, so an absent or inactive row
/// simply leaves the wallet creation step out (the ambassador can
/// configure their wallet manually from the BizCenter).
/// Routes: /api/v1/admin/payout-defaults/*
/// </summary>
[ApiController]
[Route("api/v1/admin/payout-defaults")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminPayoutDefaultsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminPayoutDefaultsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        var rows = await _db.CountryPayoutDefaults.AsNoTracking()
            .Join(_db.Countries.AsNoTracking(),
                cpd => cpd.CountryIso2,
                c   => c.Iso2,
                (cpd, c) => new CountryPayoutDefaultDto
                {
                    Id          = cpd.Id,
                    CountryIso2 = cpd.CountryIso2,
                    CountryName = c.NameEn,
                    FlagEmoji   = c.FlagEmoji,
                    WalletType  = cpd.WalletType.ToString(),
                    IsActive    = cpd.IsActive,
                    Notes       = cpd.Notes
                })
            .OrderBy(r => r.CountryName)
            .ToListAsync(ct);

        return Ok(ApiResponse<List<CountryPayoutDefaultDto>>.Ok(rows));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] UpsertCountryPayoutDefaultRequest req,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.CountryIso2) || req.CountryIso2.Length != 2)
            return BadRequest(ApiResponse<object>.Fail("INVALID_COUNTRY", "CountryIso2 must be a 2-letter ISO code."));

        if (!Enum.TryParse<WalletType>(req.WalletType, ignoreCase: true, out var walletType))
            return BadRequest(ApiResponse<object>.Fail("INVALID_WALLET_TYPE", $"Unknown wallet type '{req.WalletType}'."));

        var iso = req.CountryIso2.ToUpperInvariant();
        var countryExists = await _db.Countries.AsNoTracking().AnyAsync(c => c.Iso2 == iso, ct);
        if (!countryExists)
            return BadRequest(ApiResponse<object>.Fail("COUNTRY_NOT_FOUND", $"No Country with Iso2 '{iso}'."));

        var alreadyMapped = await _db.CountryPayoutDefaults.AnyAsync(p => p.CountryIso2 == iso, ct);
        if (alreadyMapped)
            return Conflict(ApiResponse<object>.Fail(
                "DUPLICATE_COUNTRY",
                $"Country '{iso}' already has a payout default. Use PUT /{{id}} to update it."));

        var now    = DateTime.UtcNow;
        var entity = new CountryPayoutDefault
        {
            CountryIso2  = iso,
            WalletType   = walletType,
            IsActive     = req.IsActive,
            Notes        = req.Notes,
            CreatedBy    = User.Identity?.Name ?? "admin",
            CreationDate = now,
            LastUpdateDate = now
        };
        await _db.CountryPayoutDefaults.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<int>.Ok(entity.Id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpsertCountryPayoutDefaultRequest req,
        CancellationToken ct = default)
    {
        var entity = await _db.CountryPayoutDefaults.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"CountryPayoutDefault {id} not found."));

        if (!Enum.TryParse<WalletType>(req.WalletType, ignoreCase: true, out var walletType))
            return BadRequest(ApiResponse<object>.Fail("INVALID_WALLET_TYPE", $"Unknown wallet type '{req.WalletType}'."));

        entity.WalletType     = walletType;
        entity.IsActive       = req.IsActive;
        entity.Notes          = req.Notes;
        entity.LastUpdateDate = DateTime.UtcNow;
        entity.LastUpdateBy   = User.Identity?.Name ?? "admin";
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { entity.Id }));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var entity = await _db.CountryPayoutDefaults.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"CountryPayoutDefault {id} not found."));

        _db.CountryPayoutDefaults.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    public class CountryPayoutDefaultDto
    {
        public int     Id          { get; set; }
        public string  CountryIso2 { get; set; } = string.Empty;
        public string  CountryName { get; set; } = string.Empty;
        public string  FlagEmoji   { get; set; } = string.Empty;
        public string  WalletType  { get; set; } = string.Empty;
        public bool    IsActive    { get; set; }
        public string? Notes       { get; set; }
    }

    public class UpsertCountryPayoutDefaultRequest
    {
        public string  CountryIso2 { get; set; } = string.Empty;   // ignored on PUT
        public string  WalletType  { get; set; } = string.Empty;
        public bool    IsActive    { get; set; } = true;
        public string? Notes       { get; set; }
    }
}
