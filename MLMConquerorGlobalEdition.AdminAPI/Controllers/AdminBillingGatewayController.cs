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
    private readonly IEncryptionService _crypto;

    public AdminBillingGatewayController(AppDbContext db, ICacheService cache, IEncryptionService crypto)
    {
        _crypto = crypto;
        _db    = db;
        _cache = cache;
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
    public async Task<IActionResult> GetCredential(
        string serviceKey,
        [FromQuery] string? environment = null,
        CancellationToken ct = default)
    {
        // La clave real es (ServiceKey, Environment) — hay un índice único sobre ese par.
        // Gateways como PayQuicker tienen fila de Sandbox Y de Production con la misma
        // ServiceKey, así que resolver sólo por ServiceKey devolvería una arbitraria.
        var query = _db.ApiCredentials.AsNoTracking().Where(c => c.ServiceKey == serviceKey && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(environment))
            query = query.Where(c => c.Environment == environment);

        var matches = await query.OrderBy(c => c.Environment).ToListAsync(ct);

        if (matches.Count == 0)
            return NotFound(ApiResponse<object>.Fail("CREDENTIAL_NOT_FOUND",
                $"No credential found for service key '{serviceKey}'" +
                (string.IsNullOrWhiteSpace(environment) ? "." : $" in environment '{environment}'.")));

        // Ambiguo y sin desambiguar: se rechaza en vez de adivinar. Devolver la fila
        // equivocada llevaría a que el admin edite las credenciales del ambiente que no es.
        if (matches.Count > 1)
            return BadRequest(ApiResponse<object>.Fail("CREDENTIAL_AMBIGUOUS",
                $"Service key '{serviceKey}' exists in {matches.Count} environments " +
                $"({string.Join(", ", matches.Select(m => m.Environment))}). Pass ?environment= to choose one."));

        var entity = matches[0];
        return Ok(ApiResponse<ApiCredentialMetadataDto>.Ok(new ApiCredentialMetadataDto(
            entity.Id, entity.ServiceKey, entity.Environment, entity.BaseUrl,
            entity.ApiKeyEncrypted is not null,
            entity.SecretKeyEncrypted is not null,
            entity.MerchantIdEncrypted is not null,
            entity.IsActive, entity.CreationDate,
            entity.PortalUrl,
            entity.PortalUsernameEncrypted is not null,
            entity.PortalPasswordEncrypted is not null,
            entity.AdditionalSecretEncrypted is not null)));
    }

    /// <summary>
    /// PUT upserts a credential. Los secretos viajan EN CLARO sobre TLS y los cifra el
    /// servidor con IEncryptionService antes de persistirlos.
    ///
    /// El contrato anterior exigia que el llamador mandara valores con prefijo "ENC:", lo
    /// que en la practica solo produjo una mascara: la UI concatenaba "ENC:" al texto plano
    /// y eso quedaba guardado SIN cifrar. El cifrado es responsabilidad del servidor, que es
    /// el unico con acceso al key ring.
    /// </summary>
    [HttpPut("credentials/{serviceKey}")]
    public async Task<IActionResult> UpsertCredential(
        string serviceKey, [FromBody] UpsertCredentialRequest request, CancellationToken ct = default)
    {
        // Un secreto legitimo no empieza con "ENC:". Si llega uno asi es un cliente viejo
        // mandando el prefijo a mano: se rechaza en vez de cifrar la mascara y dejar
        // guardado un valor que despues nadie va a poder usar.
        foreach (var (name, value) in new[]
                 {
                     (nameof(request.ApiKey),           request.ApiKey),
                     (nameof(request.SecretKey),        request.SecretKey),
                     (nameof(request.MerchantId),       request.MerchantId),
                     (nameof(request.AdditionalSecret), request.AdditionalSecret),
                     (nameof(request.PortalUsername),   request.PortalUsername),
                     (nameof(request.PortalPassword),   request.PortalPassword)
                 })
        {
            if (value is not null && value.StartsWith("ENC:", StringComparison.Ordinal))
                return BadRequest(ApiResponse<object>.Fail("SECRET_ALREADY_PREFIXED",
                    $"{name} must be sent in plain text over TLS; the server encrypts it. " +
                    "Remove the 'ENC:' prefix - it is applied during storage, not by the caller."));
        }

        // Cifra solo lo que vino. Null o vacio significa "no cambiar este secreto".
        string? Protect(string? plaintext) =>
            string.IsNullOrWhiteSpace(plaintext) ? null : _crypto.Encrypt(plaintext);


        var now   = DateTime.UtcNow;
        var actor = User.Identity?.Name ?? "admin";

        // El Environment del body es parte de la IDENTIDAD de la credencial, no un atributo
        // editable: (ServiceKey, Environment) tiene índice único. Buscar sólo por ServiceKey
        // haría que guardar la credencial de Sandbox pise la de Production.
        var environment = string.IsNullOrWhiteSpace(request.Environment) ? "Production" : request.Environment!.Trim();

        var entity = await _db.ApiCredentials.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ServiceKey == serviceKey && c.Environment == environment, ct);

        if (entity is null)
        {
            entity = new ApiCredential
            {
                ServiceKey   = serviceKey,
                Environment  = environment,
                BaseUrl      = request.BaseUrl,
                IsActive     = request.IsActive ?? true,
                CreatedBy    = actor,
                CreationDate = now,
                LastUpdateDate = now
            };

            entity.ApiKeyEncrypted           = Protect(request.ApiKey);
            entity.SecretKeyEncrypted        = Protect(request.SecretKey);
            entity.MerchantIdEncrypted       = Protect(request.MerchantId);
            entity.AdditionalSecretEncrypted = Protect(request.AdditionalSecret);
            entity.PortalUrl                 = request.PortalUrl;
            entity.PortalUsernameEncrypted   = Protect(request.PortalUsername);
            entity.PortalPasswordEncrypted   = Protect(request.PortalPassword);

            _db.ApiCredentials.Add(entity);
        }
        else
        {
            // Environment NO se reasigna: ya se usó para localizar la fila.
            entity.BaseUrl         = request.BaseUrl     ?? entity.BaseUrl;
            entity.IsActive        = request.IsActive    ?? entity.IsActive;
            entity.IsDeleted       = false;
            entity.LastUpdateBy    = actor;
            entity.LastUpdateDate  = now;

            // Campo vacio = conservar el secreto actual, para que el admin pueda editar la
            // URL o el flag Active sin re-tipear todos los secretos.
            if (Protect(request.ApiKey)           is { } apiKey)     entity.ApiKeyEncrypted           = apiKey;
            if (Protect(request.SecretKey)        is { } secretKey)  entity.SecretKeyEncrypted        = secretKey;
            if (Protect(request.MerchantId)       is { } merchantId) entity.MerchantIdEncrypted       = merchantId;
            if (Protect(request.AdditionalSecret) is { } additional) entity.AdditionalSecretEncrypted = additional;
            if (Protect(request.PortalUsername)   is { } portalUser) entity.PortalUsernameEncrypted   = portalUser;
            if (Protect(request.PortalPassword)   is { } portalPass) entity.PortalPasswordEncrypted   = portalPass;

            entity.PortalUrl = request.PortalUrl ?? entity.PortalUrl;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<object>.Ok(new { serviceKey, environment, updated = true }));
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
        bool HasApiKey, bool HasSecretKey, bool HasMerchantId,
        bool IsActive, DateTime CreationDate,
        // Portal administrativo del proveedor. La URL no es secreta y se devuelve tal cual;
        // usuario y contraseña sólo se reportan como "cargado / no cargado".
        string? PortalUrl = null,
        bool HasPortalUsername = false, bool HasPortalPassword = false,
        bool HasAdditionalSecret = false);

    /// <summary>
    /// Secretos EN CLARO sobre TLS: el servidor los cifra antes de guardarlos. Los nombres
    /// NO llevan el sufijo "Encrypted" a proposito, porque describen lo que el cliente
    /// envia y no como se persiste. Campo nulo o vacio = "no cambiar ese secreto".
    /// </summary>
    public record UpsertCredentialRequest(
        string? Environment, string? BaseUrl, bool? IsActive,
        string? ApiKey, string? SecretKey, string? MerchantId,
        string? AdditionalSecret = null,
        string? PortalUrl = null,
        string? PortalUsername = null, string? PortalPassword = null);

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
