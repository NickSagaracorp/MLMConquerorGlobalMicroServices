using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Tokens;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Constants;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// La pestaña de tokens de la ficha del miembro en el panel. La pantalla es la misma que la del
/// BizCenter (<c>SharedComponents/Components/Tokens/TokensPage</c>) con
/// <c>ApiBaseUrl = api/v1/admin/members/{memberId}</c>, así que cada respuesta tiene la misma forma que
/// su ruta del BizCenter y sale de las mismas consultas (<see cref="MemberTokenQueries"/>).
/// </summary>
/// <remarks>
/// SOLO LECTURA: resumen, historial y saldos. En el panel no se compra ni se distribuye (la pantalla
/// los deja apagados: nadie compra con el dinero de otro); dar o ajustar tokens tiene su propia
/// pantalla de administración. Los roles son los que ya ven la ficha del miembro.
///
/// LOS CÓDIGOS SON AL PORTADOR: quien lee uno lo canjea. Soporte (SupportManager y SupportLevel1-3) ve
/// el historial con cada código tapado (<see cref="TokenCodeMask"/>: los tres primeros caracteres y
/// <c>****</c>), también los que lleva la nota de una distribución o transferencia; solo SuperAdmin y
/// Admin lo ven entero. En el BizCenter el dueño sigue viendo los suyos completos (no pasa por aquí).
/// </remarks>
[ApiController]
[Route("api/v1/admin/members/{memberId}/tokens")]
[Authorize(Roles = "SuperAdmin,Admin,SupportManager,SupportLevel1,SupportLevel2,SupportLevel3")]
public class AdminMemberTokensController : ControllerBase
{
    private const int MaxPageSize = 100;

    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _clock;

    public AdminMemberTokensController(AppDbContext db, IDateTimeProvider clock)
    {
        _db    = db;
        _clock = clock;
    }

    /// <summary>GET api/v1/admin/members/{memberId}/tokens — historial paginado, como el del BizCenter.</summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        string memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var (items, total) = await MemberTokenQueries.HistoryPageAsync(_db, memberId, page, pageSize, ct);
        if (!CanSeeFullCodes())
            foreach (var row in items)
            {
                row.TokenCode = TokenCodeMask.Mask(row.TokenCode);
                row.Notes     = TokenCodeMask.MaskCodesIn(row.Notes);
            }

        return Ok(ApiResponse<PagedResult<MemberTokenQueries.HistoryRow>>.Ok(new PagedResult<MemberTokenQueries.HistoryRow>
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        }));
    }

    private bool CanSeeFullCodes() => User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.Admin);

    /// <summary>GET api/v1/admin/members/{memberId}/tokens/summary — total, usados y disponibles.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(string memberId, CancellationToken ct = default)
    {
        var counts = await MemberTokenQueries.Counts(_db, memberId, _clock.Now).FirstOrDefaultAsync(ct);
        return Ok(ApiResponse<TokenSummaryDto>.Ok(new TokenSummaryDto
        {
            TotalTokens    = counts?.Total ?? 0,
            TotalUsed      = counts?.Used ?? 0,
            TotalAvailable = counts?.Available ?? 0
        }));
    }

    /// <summary>GET api/v1/admin/members/{memberId}/tokens/balances — saldo por tipo de token.</summary>
    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(string memberId, CancellationToken ct = default)
    {
        var balances = await MemberTokenQueries.Balances(_db, memberId).ToListAsync(ct);
        return Ok(ApiResponse<List<MemberTokenQueries.BalanceRow>>.Ok(balances));
    }
}
