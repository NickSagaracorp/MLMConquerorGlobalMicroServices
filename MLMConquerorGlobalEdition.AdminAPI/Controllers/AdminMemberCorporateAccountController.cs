using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// PUT api/v1/admin/members/{memberId}/corporate-account — marca o desmarca la cuenta corporativa. Los reportes que no
/// deben mostrar cuentas de la empresa las excluyen por este indicador. Cada cambio deja una fila en GeneralAuditTracking.
/// </summary>
[ApiController]
[Route("api/v1/admin/members/{memberId}/corporate-account")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminMemberCorporateAccountController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly IDateTimeProvider   _clock;
    private readonly ICurrentUserService _currentUser;

    public AdminMemberCorporateAccountController(AppDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db          = db;
        _clock       = clock;
        _currentUser = currentUser;
    }

    public sealed record SetRequest(bool IsCorporateAccount);

    [HttpPut]
    public async Task<IActionResult> Set(string memberId, [FromBody] SetRequest request, CancellationToken ct)
    {
        var member = await _db.MemberProfiles.FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
        if (member is null)
            return NotFound(ApiResponse<object>.Fail("MEMBER_NOT_FOUND", "Member not found."));

        if (member.IsCorporateAccount != request.IsCorporateAccount)
        {
            var now = _clock.Now;
            _db.AuditTracking.Add(new GeneralAuditTracking
            {
                EntityName = "MemberProfile",
                EntityId   = memberId,
                Action     = "CorporateAccountChanged",
                OldValues  = $"{{\"IsCorporateAccount\":{member.IsCorporateAccount.ToString().ToLowerInvariant()}}}",
                NewValues  = $"{{\"IsCorporateAccount\":{request.IsCorporateAccount.ToString().ToLowerInvariant()}}}",
                ChangedBy  = _currentUser.UserId,
                ChangedAt  = now
            });
            member.IsCorporateAccount = request.IsCorporateAccount;
            member.LastUpdateDate     = now;
            member.LastUpdateBy       = _currentUser.UserId;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(ApiResponse<object>.Ok(new { member.MemberId, member.IsCorporateAccount }));
    }
}
