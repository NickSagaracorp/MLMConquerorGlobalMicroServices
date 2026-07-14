using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IEncryptionService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEncryptionService;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin endpoints for the billing gateway rotation engine.
/// All routes under /api/v1/admin/billing/...
/// </summary>
[ApiController]
[Route("api/v1/admin/billing")]
[Authorize(Roles = "SuperAdmin,Admin,BillingManager")]
public class AdminBillingGatewayController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly IEncryptionService _encryption;

    public AdminBillingGatewayController(AppDbContext db, ICacheService cache, IEncryptionService encryption)
    {
        _db         = db;
        _cache      = cache;
        _encryption = encryption;
    }

    // ── Gateways ───────────────────────────────────────────────────────────

    /// <summary>GET /api/v1/admin/billing/gateways — list all processor catalog entries.</summary>
    [HttpGet("gateways")]
    public async Task<IActionResult> GetGateways(CancellationToken ct = default)
    {
        var items = await _db.GatewayCatalog
            .AsNoTracking()
            .OrderBy(x => x.Processor)
            .Select(x => new GatewayCatalogDto(
                x.Id, x.Processor, x.Processor.ToString(), x.DisplayName,
                x.IsActive, x.SupportsRefund, x.SupportsRecurring))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<GatewayCatalogDto>>.Ok(items));
    }

    /// <summary>PUT /api/v1/admin/billing/gateways/{id} — activate / deactivate a gateway entry.</summary>
    [HttpPut("gateways/{id:int}")]
    public async Task<IActionResult> UpdateGateway(
        int id, [FromBody] UpdateGatewayRequest request, CancellationToken ct = default)
    {
        var entity = await _db.GatewayCatalog.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("GATEWAY_NOT_FOUND", $"Gateway catalog entry {id} not found."));

        entity.IsActive         = request.IsActive;
        entity.DisplayName      = request.DisplayName ?? entity.DisplayName;
        entity.SupportsRefund   = request.SupportsRefund ?? entity.SupportsRefund;
        entity.SupportsRecurring = request.SupportsRecurring ?? entity.SupportsRecurring;
        entity.LastUpdateBy     = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate   = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { id, entity.IsActive }));
    }

    // ── Routing Rules ──────────────────────────────────────────────────────

    [HttpGet("routing-rules")]
    public async Task<IActionResult> GetRoutingRules(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _db.GatewayRoutingRules
            .AsNoTracking()
            .Include(r => r.Splits)
            .Include(r => r.CurrencyPolicy)
            .OrderBy(r => r.OperationType).ThenBy(r => r.CardBrand).ThenBy(r => r.Id);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => ToRoutingRuleDto(r))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<RoutingRuleDto>>.Ok(new PagedResult<RoutingRuleDto>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        }));
    }

    [HttpGet("routing-rules/{id:int}")]
    public async Task<IActionResult> GetRoutingRule(int id, CancellationToken ct = default)
    {
        var entity = await _db.GatewayRoutingRules
            .Include(r => r.Splits)
            .Include(r => r.CurrencyPolicy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("RULE_NOT_FOUND", $"Routing rule {id} not found."));

        return Ok(ApiResponse<RoutingRuleDto>.Ok(ToRoutingRuleDto(entity)));
    }

    [HttpPost("routing-rules")]
    public async Task<IActionResult> CreateRoutingRule(
        [FromBody] CreateRoutingRuleRequest request, CancellationToken ct = default)
    {
        var validationError = ValidateSplits(request.Splits);
        if (validationError is not null)
            return BadRequest(ApiResponse<object>.Fail("INVALID_SPLITS", validationError));

        var entity = new GatewayRoutingRule
        {
            OperationType    = request.OperationType,
            CardBrand        = request.CardBrand,
            IsoCountryCode   = request.IsoCountryCode?.ToUpperInvariant(),
            CountryGroupId   = request.CountryGroupId,
            IsCatchAll       = request.IsCatchAll,
            CurrencyPolicyId = request.CurrencyPolicyId,
            IsActive         = request.IsActive,
            CreatedBy        = User.Identity?.Name ?? "admin",
            CreationDate     = DateTime.UtcNow
        };

        foreach (var s in request.Splits)
            entity.Splits.Add(new GatewayRoutingRuleSplit
            {
                CardProcessor = s.CardProcessor,
                WeightPercent = s.WeightPercent,
                SortOrder     = s.SortOrder,
                CreatedBy     = User.Identity?.Name ?? "admin",
                CreationDate  = DateTime.UtcNow
            });

        _db.GatewayRoutingRules.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetRoutingRule), new { id = entity.Id },
            ApiResponse<RoutingRuleDto>.Ok(ToRoutingRuleDto(entity)));
    }

    [HttpPut("routing-rules/{id:int}")]
    public async Task<IActionResult> UpdateRoutingRule(
        int id, [FromBody] CreateRoutingRuleRequest request, CancellationToken ct = default)
    {
        var entity = await _db.GatewayRoutingRules
            .Include(r => r.Splits)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("RULE_NOT_FOUND", $"Routing rule {id} not found."));

        var validationError = ValidateSplits(request.Splits);
        if (validationError is not null)
            return BadRequest(ApiResponse<object>.Fail("INVALID_SPLITS", validationError));

        entity.OperationType    = request.OperationType;
        entity.CardBrand        = request.CardBrand;
        entity.IsoCountryCode   = request.IsoCountryCode?.ToUpperInvariant();
        entity.CountryGroupId   = request.CountryGroupId;
        entity.IsCatchAll       = request.IsCatchAll;
        entity.CurrencyPolicyId = request.CurrencyPolicyId;
        entity.IsActive         = request.IsActive;
        entity.LastUpdateBy     = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate   = DateTime.UtcNow;

        _db.GatewayRoutingRuleSplits.RemoveRange(entity.Splits);
        entity.Splits.Clear();
        foreach (var s in request.Splits)
            entity.Splits.Add(new GatewayRoutingRuleSplit
            {
                CardProcessor = s.CardProcessor,
                WeightPercent = s.WeightPercent,
                SortOrder     = s.SortOrder,
                CreatedBy     = User.Identity?.Name ?? "admin",
                CreationDate  = DateTime.UtcNow
            });

        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<RoutingRuleDto>.Ok(ToRoutingRuleDto(entity)));
    }

    [HttpDelete("routing-rules/{id:int}")]
    public async Task<IActionResult> DeleteRoutingRule(int id, CancellationToken ct = default)
    {
        var entity = await _db.GatewayRoutingRules
            .Include(r => r.Splits)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("RULE_NOT_FOUND", $"Routing rule {id} not found."));

        entity.IsActive       = false;
        entity.LastUpdateBy   = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { id, isActive = false }, "Routing rule deactivated."));
    }

    // ── Country Groups ─────────────────────────────────────────────────────

    [HttpGet("country-groups")]
    public async Task<IActionResult> GetCountryGroups(CancellationToken ct = default)
    {
        var items = await _db.CountryGroups
            .AsNoTracking()
            .Include(g => g.Countries)
            .OrderBy(g => g.Code)
            .Select(g => new CountryGroupDto(
                g.Id, g.Code, g.Name,
                g.Countries.Select(c => c.IsoCountryCode).ToList()))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<CountryGroupDto>>.Ok(items));
    }

    [HttpGet("country-groups/{id:int}")]
    public async Task<IActionResult> GetCountryGroup(int id, CancellationToken ct = default)
    {
        var entity = await _db.CountryGroups
            .AsNoTracking()
            .Include(g => g.Countries)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("GROUP_NOT_FOUND", $"Country group {id} not found."));

        return Ok(ApiResponse<CountryGroupDto>.Ok(
            new CountryGroupDto(entity.Id, entity.Code, entity.Name,
                entity.Countries.Select(c => c.IsoCountryCode).ToList())));
    }

    [HttpPost("country-groups")]
    public async Task<IActionResult> CreateCountryGroup(
        [FromBody] CountryGroupFormRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";

        var entity = new CountryGroup
        {
            Code         = request.Code.Trim().ToUpperInvariant(),
            Name         = request.Name.Trim(),
            CreatedBy    = actor,
            CreationDate = now
        };
        foreach (var iso in request.IsoCodes.Distinct())
            entity.Countries.Add(new CountryGroupCountry
            {
                IsoCountryCode = iso.Trim().ToUpperInvariant(),
                CreatedBy      = actor,
                CreationDate   = now
            });

        _db.CountryGroups.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetCountryGroup), new { id = entity.Id },
            ApiResponse<CountryGroupDto>.Ok(
                new CountryGroupDto(entity.Id, entity.Code, entity.Name,
                    entity.Countries.Select(c => c.IsoCountryCode).ToList())));
    }

    [HttpPut("country-groups/{id:int}")]
    public async Task<IActionResult> UpdateCountryGroup(
        int id, [FromBody] CountryGroupFormRequest request, CancellationToken ct = default)
    {
        var entity = await _db.CountryGroups
            .Include(g => g.Countries)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("GROUP_NOT_FOUND", $"Country group {id} not found."));

        var now = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";

        entity.Code           = request.Code.Trim().ToUpperInvariant();
        entity.Name           = request.Name.Trim();
        entity.LastUpdateBy   = actor;
        entity.LastUpdateDate = now;

        _db.CountryGroupCountries.RemoveRange(entity.Countries);
        entity.Countries.Clear();
        foreach (var iso in request.IsoCodes.Distinct())
            entity.Countries.Add(new CountryGroupCountry
            {
                IsoCountryCode = iso.Trim().ToUpperInvariant(),
                CreatedBy      = actor,
                CreationDate   = now
            });

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<CountryGroupDto>.Ok(
            new CountryGroupDto(entity.Id, entity.Code, entity.Name,
                entity.Countries.Select(c => c.IsoCountryCode).ToList())));
    }

    [HttpDelete("country-groups/{id:int}")]
    public async Task<IActionResult> DeleteCountryGroup(int id, CancellationToken ct = default)
    {
        var entity = await _db.CountryGroups
            .Include(g => g.Countries)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("GROUP_NOT_FOUND", $"Country group {id} not found."));

        _db.CountryGroupCountries.RemoveRange(entity.Countries);
        _db.CountryGroups.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { id }, "Country group deleted."));
    }

    // ── Currency Policies ──────────────────────────────────────────────────

    [HttpGet("currency-policies")]
    public async Task<IActionResult> GetCurrencyPolicies(CancellationToken ct = default)
    {
        var items = await _db.CurrencyPolicies
            .AsNoTracking()
            .OrderBy(p => p.PresentmentCurrency)
            .Select(p => new CurrencyPolicyDto(p.Id, p.PresentmentCurrency, p.MarkupPercent, p.IsActive, p.Description))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<CurrencyPolicyDto>>.Ok(items));
    }

    [HttpPost("currency-policies")]
    public async Task<IActionResult> CreateCurrencyPolicy(
        [FromBody] CurrencyPolicyFormRequest request, CancellationToken ct = default)
    {
        if (await _db.CurrencyPolicies.AnyAsync(p => p.PresentmentCurrency == request.PresentmentCurrency.ToUpperInvariant(), ct))
            return Conflict(ApiResponse<object>.Fail("DUPLICATE_POLICY",
                $"Currency policy for '{request.PresentmentCurrency}' already exists."));

        var entity = new CurrencyPolicy
        {
            PresentmentCurrency = request.PresentmentCurrency.ToUpperInvariant(),
            MarkupPercent       = request.MarkupPercent,
            IsActive            = request.IsActive,
            Description         = request.Description,
            CreatedBy           = User.Identity?.Name ?? "admin",
            CreationDate        = DateTime.UtcNow
        };

        _db.CurrencyPolicies.Add(entity);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetCurrencyPolicies), null,
            ApiResponse<CurrencyPolicyDto>.Ok(
                new CurrencyPolicyDto(entity.Id, entity.PresentmentCurrency, entity.MarkupPercent, entity.IsActive, entity.Description)));
    }

    [HttpPut("currency-policies/{id:int}")]
    public async Task<IActionResult> UpdateCurrencyPolicy(
        int id, [FromBody] CurrencyPolicyFormRequest request, CancellationToken ct = default)
    {
        var entity = await _db.CurrencyPolicies.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("POLICY_NOT_FOUND", $"Currency policy {id} not found."));

        entity.PresentmentCurrency = request.PresentmentCurrency.ToUpperInvariant();
        entity.MarkupPercent       = request.MarkupPercent;
        entity.IsActive            = request.IsActive;
        entity.Description         = request.Description;
        entity.LastUpdateBy        = User.Identity?.Name ?? "admin";
        entity.LastUpdateDate      = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<CurrencyPolicyDto>.Ok(
            new CurrencyPolicyDto(entity.Id, entity.PresentmentCurrency, entity.MarkupPercent, entity.IsActive, entity.Description)));
    }

    // ── API Credentials ────────────────────────────────────────────────────

    /// <summary>GET returns metadata only — secrets are NEVER returned in plain text.</summary>
    [HttpGet("credentials/{serviceKey}")]
    public async Task<IActionResult> GetCredential(string serviceKey, CancellationToken ct = default)
    {
        var entity = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == serviceKey && !c.IsDeleted, ct);

        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("CREDENTIAL_NOT_FOUND",
                $"No credential found for service key '{serviceKey}'."));

        return Ok(ApiResponse<ApiCredentialMetadataDto>.Ok(new ApiCredentialMetadataDto(
            entity.Id, entity.ServiceKey, entity.Environment, entity.BaseUrl,
            entity.ApiKeyEncrypted is not null,
            entity.SecretKeyEncrypted is not null,
            entity.MerchantIdEncrypted is not null,
            entity.SpreedlyGatewayTokenEncrypted is not null,
            entity.IsActive, entity.CreationDate)));
    }

    /// <summary>
    /// PUT upserts credential — secrets are sent as PLAINTEXT and encrypted server-side
    /// before being persisted (never stored or logged in plain text).
    /// </summary>
    [HttpPut("credentials/{serviceKey}")]
    public async Task<IActionResult> UpsertCredential(
        string serviceKey, [FromBody] UpsertCredentialRequest request, CancellationToken ct = default)
    {
        var now   = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";

        var entity = await _db.ApiCredentials.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ServiceKey == serviceKey, ct);

        if (entity is null)
        {
            entity = new ApiCredential
            {
                ServiceKey   = serviceKey,
                Environment  = request.Environment ?? "Production",
                BaseUrl      = request.BaseUrl,
                IsActive     = request.IsActive ?? true,
                CreatedBy    = actor,
                CreationDate = now,
                LastUpdateDate = now
            };

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
                entity.ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey);
            if (!string.IsNullOrWhiteSpace(request.SecretKey))
                entity.SecretKeyEncrypted = _encryption.Encrypt(request.SecretKey);
            if (!string.IsNullOrWhiteSpace(request.MerchantId))
                entity.MerchantIdEncrypted = _encryption.Encrypt(request.MerchantId);
            if (!string.IsNullOrWhiteSpace(request.SpreedlyGatewayToken))
                entity.SpreedlyGatewayTokenEncrypted = _encryption.Encrypt(request.SpreedlyGatewayToken);

            _db.ApiCredentials.Add(entity);
        }
        else
        {
            entity.Environment     = request.Environment ?? entity.Environment;
            entity.BaseUrl         = request.BaseUrl     ?? entity.BaseUrl;
            entity.IsActive        = request.IsActive    ?? entity.IsActive;
            entity.IsDeleted       = false;
            entity.LastUpdateBy    = actor;
            entity.LastUpdateDate  = now;

            if (!string.IsNullOrWhiteSpace(request.ApiKey))
                entity.ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey);
            if (!string.IsNullOrWhiteSpace(request.SecretKey))
                entity.SecretKeyEncrypted = _encryption.Encrypt(request.SecretKey);
            if (!string.IsNullOrWhiteSpace(request.MerchantId))
                entity.MerchantIdEncrypted = _encryption.Encrypt(request.MerchantId);
            if (!string.IsNullOrWhiteSpace(request.SpreedlyGatewayToken))
                entity.SpreedlyGatewayTokenEncrypted = _encryption.Encrypt(request.SpreedlyGatewayToken);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { serviceKey, updated = true }));
    }

    // ── Routing Counters (observability) ───────────────────────────────────

    [HttpGet("routing-counters")]
    public async Task<IActionResult> GetRoutingCounters(
        [FromQuery] string? bucketKey = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.GatewayRoutingCounters.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(bucketKey))
            query = query.Where(c => c.RouteBucketKey == bucketKey);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.RouteBucketKey).ThenBy(c => c.CardProcessor)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new RoutingCounterDto(c.RouteBucketKey, c.CardProcessor, c.CardProcessor.ToString(), c.AttemptCount))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<RoutingCounterDto>>.Ok(new PagedResult<RoutingCounterDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    // ── Charge Attempts (audit) ────────────────────────────────────────────

    [HttpGet("charge-attempts")]
    public async Task<IActionResult> GetChargeAttempts(
        [FromQuery] string? memberId     = null,
        [FromQuery] string? outcome      = null,
        [FromQuery] int     page         = 1,
        [FromQuery] int     pageSize     = 20,
        CancellationToken ct = default)
    {
        var query = _db.GatewayChargeAttempts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(memberId)) query = query.Where(a => a.MemberId == memberId);
        if (!string.IsNullOrWhiteSpace(outcome))  query = query.Where(a => a.Outcome  == outcome);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AttemptedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ChargeAttemptDto(
                a.Id, a.MemberId, a.CardProcessor, a.CardProcessor.ToString(),
                a.FallbackStepIndex, a.PresentmentCurrency, a.OriginalAmountUsd,
                a.ConvertedAmount, a.Outcome, a.GatewayTransactionId,
                a.PaymentHistoryId, a.FailureReason, a.AttemptedAtUtc))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<ChargeAttemptDto>>.Ok(new PagedResult<ChargeAttemptDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        }));
    }

    // ── Exchange Rates ─────────────────────────────────────────────────────

    /// <summary>POST /api/v1/admin/billing/exchange-rates/refresh — manual trigger.</summary>
    [HttpPost("exchange-rates/refresh")]
    public async Task<IActionResult> RefreshExchangeRates(CancellationToken ct = default)
    {
        var snapshots = await _db.ExchangeRateSnapshots
            .AsNoTracking()
            .GroupBy(s => s.QuoteCurrency)
            .Select(g => g.OrderByDescending(s => s.FetchedAtUtc).First())
            .Select(s => new ExchangeRateDto(s.QuoteCurrency, s.Rate, s.FetchedAtUtc))
            .ToListAsync(ct);

        // Note: this endpoint returns the last known rates. A Hangfire job triggers the actual refresh.
        return Ok(ApiResponse<IEnumerable<ExchangeRateDto>>.Ok(snapshots,
            "Last known exchange rates returned. Use the Hangfire dashboard to trigger a live refresh."));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string? ValidateSplits(IEnumerable<SplitEntryRequest> splits)
    {
        var list = splits.ToList();
        if (list.Count == 0) return "At least one split is required.";
        var total = list.Sum(s => s.WeightPercent);
        if (total != 100m) return $"Split weights must sum to 100. Current sum: {total}.";
        return null;
    }

    private static RoutingRuleDto ToRoutingRuleDto(GatewayRoutingRule r) => new(
        r.Id, r.OperationType, r.CardBrand, r.IsoCountryCode, r.CountryGroupId,
        r.IsCatchAll, r.CurrencyPolicyId,
        r.CurrencyPolicy?.PresentmentCurrency, r.IsActive,
        r.Splits.OrderBy(s => s.SortOrder)
                .Select(s => new SplitEntryRequest(s.CardProcessor, s.WeightPercent, s.SortOrder))
                .ToList());

    // ── DTOs ───────────────────────────────────────────────────────────────

    public record GatewayCatalogDto(
        int Id, CardProcessor Processor, string ProcessorName, string DisplayName,
        bool IsActive, bool SupportsRefund, bool SupportsRecurring);

    public record UpdateGatewayRequest(
        bool IsActive, string? DisplayName = null,
        bool? SupportsRefund = null, bool? SupportsRecurring = null);

    public record RoutingRuleDto(
        int Id, BillingOperationType OperationType, CardBrand? CardBrand,
        string? IsoCountryCode, int? CountryGroupId, bool IsCatchAll,
        int? CurrencyPolicyId, string? PresentmentCurrency, bool IsActive,
        List<SplitEntryRequest> Splits);

    public record SplitEntryRequest(CardProcessor CardProcessor, decimal WeightPercent, int SortOrder);

    public record CreateRoutingRuleRequest(
        BillingOperationType OperationType, CardBrand? CardBrand,
        string? IsoCountryCode, int? CountryGroupId, bool IsCatchAll,
        int? CurrencyPolicyId, bool IsActive,
        List<SplitEntryRequest> Splits);

    public record CountryGroupDto(int Id, string Code, string Name, List<string> IsoCodes);

    public record CountryGroupFormRequest(string Code, string Name, List<string> IsoCodes);

    public record CurrencyPolicyDto(
        int Id, string PresentmentCurrency, decimal MarkupPercent, bool IsActive, string? Description);

    public record CurrencyPolicyFormRequest(
        string PresentmentCurrency, decimal MarkupPercent, bool IsActive, string? Description);

    public record ApiCredentialMetadataDto(
        string Id, string ServiceKey, string Environment, string? BaseUrl,
        bool HasApiKey, bool HasSecretKey, bool HasMerchantId, bool HasSpreedlyGatewayToken,
        bool IsActive, DateTime CreationDate);

    /// <summary>
    /// Secrets are supplied as PLAINTEXT here — the controller encrypts them server-side
    /// via IEncryptionService before persisting. Never sent as "ENC:"-prefixed values by the caller.
    /// </summary>
    public record UpsertCredentialRequest(
        string? Environment, string? BaseUrl, bool? IsActive,
        string? ApiKey, string? SecretKey, string? MerchantId, string? SpreedlyGatewayToken);

    public record RoutingCounterDto(
        string RouteBucketKey, CardProcessor CardProcessor, string ProcessorName, long AttemptCount);

    public record ChargeAttemptDto(
        long Id, string MemberId, CardProcessor CardProcessor, string ProcessorName,
        int FallbackStepIndex, string PresentmentCurrency,
        decimal OriginalAmountUsd, decimal ConvertedAmount,
        string Outcome, string? GatewayTransactionId, string? PaymentHistoryId,
        string? FailureReason, DateTime AttemptedAtUtc);

    public record ExchangeRateDto(string Currency, decimal Rate, DateTime FetchedAtUtc);
}
