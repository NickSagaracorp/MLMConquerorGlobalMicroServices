using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
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

        var now    = DateTime.UtcNow;
        var actor  = User.Identity?.Name ?? "admin";
        var oldWalletType = entity.WalletType;

        entity.WalletType     = walletType;
        entity.IsActive       = req.IsActive;
        entity.Notes          = req.Notes;
        entity.LastUpdateDate = now;
        entity.LastUpdateBy   = actor;

        var retroactiveCount = 0;
        if (req.ApplyRetroactively && oldWalletType != walletType)
            retroactiveCount = await ApplyRetroactiveMigrationAsync(
                entity.CountryIso2, oldWalletType, walletType, actor, now, ct);

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            entity.Id,
            RetroactiveWalletsUpdated = retroactiveCount
        }));
    }

    /// <summary>
    /// Preview the impact of a retroactive gateway swap BEFORE the PUT is issued.
    /// Returns the wallet count that would be migrated so the admin modal can show
    /// "5 ambassadors would migrate from Dwolla → Paypal" before they confirm.
    /// </summary>
    [HttpGet("{id:int}/retroactive-preview")]
    public async Task<IActionResult> RetroactivePreview(
        int id, [FromQuery] string newWalletType, CancellationToken ct = default)
    {
        var entity = await _db.CountryPayoutDefaults.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"CountryPayoutDefault {id} not found."));

        if (!Enum.TryParse<WalletType>(newWalletType, ignoreCase: true, out var newType))
            return BadRequest(ApiResponse<object>.Fail("INVALID_WALLET_TYPE", $"Unknown wallet type '{newWalletType}'."));

        var count = entity.WalletType == newType
            ? 0
            : await CountRetroactiveCandidatesAsync(entity.CountryIso2, entity.WalletType, ct);

        return Ok(ApiResponse<RetroactivePreviewDto>.Ok(new RetroactivePreviewDto
        {
            CountryIso2          = entity.CountryIso2,
            OldWalletType        = entity.WalletType.ToString(),
            NewWalletType        = newType.ToString(),
            AffectedWalletCount  = count
        }));
    }

    private Task<int> CountRetroactiveCandidatesAsync(
        string countryIso2, WalletType oldType, CancellationToken ct)
        => (from w in _db.Wallets
            join m in _db.MemberProfiles on w.MemberId equals m.MemberId
            where m.Country == countryIso2
               && w.WalletType == oldType
               && w.Status     != WalletStatus.Rejected
            select w.Id).CountAsync(ct);

    /// <summary>
    /// Migrate every MemberProfilesWallet whose member lives in the given country
    /// AND currently uses the old default AND is not Rejected. Each migration
    /// writes a WalletTypeChanged audit row carrying the reason. Caller is
    /// responsible for SaveChangesAsync — both the wallet updates and the new
    /// default value land in one transaction.
    /// </summary>
    private async Task<int> ApplyRetroactiveMigrationAsync(
        string countryIso2, WalletType oldType, WalletType newType,
        string actor, DateTime now, CancellationToken ct)
    {
        var wallets = await (
            from w in _db.Wallets
            join m in _db.MemberProfiles on w.MemberId equals m.MemberId
            where m.Country == countryIso2
               && w.WalletType == oldType
               && w.Status     != WalletStatus.Rejected
            select w).ToListAsync(ct);

        if (wallets.Count == 0) return 0;

        var reason = $"Country {countryIso2} default gateway changed: " +
                     $"{oldType} → {newType} (retroactive update by {actor}).";

        foreach (var w in wallets)
        {
            w.WalletType     = newType;
            w.LastUpdateDate = now;
            w.LastUpdateBy   = actor;

            _db.WalletHistories.Add(new MemberProfilesWalletHistory
            {
                WalletId             = w.Id,
                MemberId             = w.MemberId,
                WalletType           = newType,
                Action               = WalletHistoryAction.WalletTypeChanged,
                OldStatus            = w.Status,
                NewStatus            = w.Status,
                OldAccountIdentifier = w.AccountIdentifier,
                NewAccountIdentifier = w.AccountIdentifier,
                OldIsPreferred       = w.IsPreferred,
                NewIsPreferred       = w.IsPreferred,
                ChangeReason         = reason,
                CreatedBy            = actor,
                CreationDate         = now
            });
        }

        return wallets.Count;
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

        /// <summary>
        /// PUT-only. When true and the WalletType is actually changing, every
        /// existing non-Rejected wallet in this country that currently matches the
        /// PREVIOUS default gets its WalletType swapped to the new default and an
        /// audit row is written. Wallets the member explicitly picked away from
        /// the default (different WalletType) and Rejected wallets are untouched.
        /// </summary>
        public bool    ApplyRetroactively { get; set; }
    }

    public class RetroactivePreviewDto
    {
        public string CountryIso2         { get; set; } = string.Empty;
        public string OldWalletType       { get; set; } = string.Empty;
        public string NewWalletType       { get; set; } = string.Empty;
        public int    AffectedWalletCount { get; set; }
    }
}
