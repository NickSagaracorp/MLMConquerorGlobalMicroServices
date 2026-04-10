using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.CreateCorporatePromo;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.DeleteCorporatePromo;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromoById;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromos;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoMembers;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetPromoStats;
using MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.UpdateCorporatePromo;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/corporate-promos")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class CorporatePromosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;

    public CorporatePromosController(IMediator mediator, AppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetCorporatePromosQuery(new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<PagedResult<CorporatePromoDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<CorporatePromoDto>>.Ok(result.Value!));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCorporatePromoByIdQuery(id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<CorporatePromoDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<CorporatePromoDto>.Ok(result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCorporatePromoRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateCorporatePromoCommand(request), ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<string>.Ok(result.Value!, "Corporate promo created."));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateCorporatePromoRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateCorporatePromoCommand(id, request), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Corporate promo updated."));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DeleteCorporatePromoCommand(id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<bool>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<bool>.Ok(true, "Corporate promo deactivated."));
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetPromoMembersQuery(id, new PagedRequest { Page = page, PageSize = pageSize }), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<PagedResult<PromoMemberDto>>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PagedResult<PromoMemberDto>>.Ok(result.Value!));
    }

    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetStats(string id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPromoStatsQuery(id), ct);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<PromoStatsDto>.Fail(result.ErrorCode!, result.Error!));

        return Ok(ApiResponse<PromoStatsDto>.Ok(result.Value!));
    }


    [HttpGet("{id}/product-commissions")]
    public async Task<IActionResult> GetProductCommissions(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _db.ProductCommissionPromos.AsNoTracking()
            .Include(p => p.Product)
            .Where(p => p.CorporatePromo.Id == id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PromoProductCommissionDto
            {
                Id = p.Id,
                ProductId = p.Product.Id,
                ProductName = p.Product.Name,
                TriggerSponsorBonus = p.TriggerSponsorBonus,
                TriggerBuilderBonus = p.TriggerBuilderBonus,
                TriggerFastStartBonus = p.TriggerFastStartBonus,
                TriggerBoostBonus = p.TriggerBoostBonus,
                CarBonusEligible = p.CarBonusEligible,
                PresidentialBonusEligible = p.PresidentialBonusEligible,
                EligibleMembershipResidual = p.EligibleMembershipResidual,
                EligibleDailyResidual = p.EligibleDailyResidual,
                IsActive = true
            })
            .ToListAsync(ct);

        var paged = new PagedResult<PromoProductCommissionDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ApiResponse<PagedResult<PromoProductCommissionDto>>.Ok(paged));
    }

    [HttpPost("{id}/product-commissions")]
    public async Task<IActionResult> CreateProductCommission(
        string id,
        [FromBody] UpsertPromoProductCommissionRequest request,
        CancellationToken ct = default)
    {
        var promo = await _db.CorporatePromos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null)
            return NotFound(ApiResponse<object>.Fail("PROMO_NOT_FOUND", "Promo not found."));

        var entity = new ProductCommissionPromo
        {
            ProductId = request.ProductId,
            TriggerSponsorBonus = request.TriggerSponsorBonus,
            TriggerBuilderBonus = request.TriggerBuilderBonus,
            TriggerFastStartBonus = request.TriggerFastStartBonus,
            TriggerBoostBonus = request.TriggerBoostBonus,
            CarBonusEligible = request.CarBonusEligible,
            PresidentialBonusEligible = request.PresidentialBonusEligible,
            EligibleMembershipResidual = request.EligibleMembershipResidual,
            EligibleDailyResidual = request.EligibleDailyResidual,
            CreatedBy = User.Identity?.Name ?? "admin",
            CreationDate = DateTime.UtcNow
        };

        // Link to promo via navigation
        _db.Entry(entity).Property("CorporatePromoId1").CurrentValue = id;
        await _db.ProductCommissionPromos.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { entity.Id }, "Created."));
    }

    [HttpPut("{id}/product-commissions/{ruleId:int}")]
    public async Task<IActionResult> UpdateProductCommission(
        string id, int ruleId,
        [FromBody] UpsertPromoProductCommissionRequest request,
        CancellationToken ct = default)
    {
        var entity = await _db.ProductCommissionPromos
            .FirstOrDefaultAsync(p => p.Id == ruleId && p.CorporatePromo.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Rule not found."));

        entity.ProductId = request.ProductId;
        entity.TriggerSponsorBonus = request.TriggerSponsorBonus;
        entity.TriggerBuilderBonus = request.TriggerBuilderBonus;
        entity.TriggerFastStartBonus = request.TriggerFastStartBonus;
        entity.TriggerBoostBonus = request.TriggerBoostBonus;
        entity.CarBonusEligible = request.CarBonusEligible;
        entity.PresidentialBonusEligible = request.PresidentialBonusEligible;
        entity.EligibleMembershipResidual = request.EligibleMembershipResidual;
        entity.EligibleDailyResidual = request.EligibleDailyResidual;
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Updated."));
    }

    [HttpDelete("{id}/product-commissions/{ruleId:int}")]
    public async Task<IActionResult> DeleteProductCommission(
        string id, int ruleId, CancellationToken ct = default)
    {
        var entity = await _db.ProductCommissionPromos
            .FirstOrDefaultAsync(p => p.Id == ruleId && p.CorporatePromo.Id == id, ct);
        if (entity is null)
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Rule not found."));

        _db.ProductCommissionPromos.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Deleted."));
    }
}
