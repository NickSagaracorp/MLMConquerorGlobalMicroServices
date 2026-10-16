using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

/// <summary>
/// Admin tooling for signups stuck in Pending status — typically zombies left
/// behind by a crash during the original Complete-Signup step.
/// </summary>
[ApiController]
[Route("api/v1/admin/signups")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class AdminSignupsController : ControllerBase
{
    private readonly AppDbContext _db;

    /// <summary>El reintento de alta ya no existe. 410.</summary>
    public const string RetryRetiredCode = "SIGNUP_RETRY_RETIRED";

    public AdminSignupsController(AppDbContext db) => _db = db;

    /// <summary>GET /api/v1/admin/signups/pending — lists members stuck in Pending status with their pending order.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> ListPending(CancellationToken ct = default)
    {
        var rows = await (
            from m in _db.MemberProfiles.AsNoTracking()
            where !m.IsDeleted && m.Status == MemberAccountStatus.Pending
            join o in _db.Orders.AsNoTracking()
                on m.MemberId equals o.MemberId
            where o.Status == OrderStatus.Pending
            orderby m.CreationDate descending
            select new PendingSignupDto(
                m.MemberId,
                (m.FirstName + " " + m.LastName).Trim(),
                m.Email,
                m.Country,
                m.SponsorMemberId,
                m.EnrollDate,
                o.Id,
                o.TotalAmount)
        ).ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<PendingSignupDto>>.Ok(rows));
    }

    /// <summary>
    /// POST /api/v1/admin/signups/{orderId}/retry-complete — RETIRADO: 410 SIGNUP_RETRY_RETIRED.
    ///
    /// Reenviaba <c>complete</c> a SignupAPI con <c>paymentMethod: 1</c> (tarjeta) SIN tarjeta, para que el
    /// alta "zombi" se activara sin cobrar. La validación de SignupAPI ya lo rechazaba siempre (exige el cuerpo
    /// <c>creditCard</c>), y ninguno de los métodos que cobran se puede repetir desde aquí: el token pide su
    /// código, la cripto su moneda y Entrax su QR. Un alta a medias se resuelve por su camino: la cripto se
    /// confirma en Crypto Signups, Entrax con Re-check, y la activación manual deliberada es el cambio de
    /// estado del miembro (PUT members/{memberId}/status), que deja rastro en MemberStatusHistory.
    /// </summary>
    [HttpPost("{orderId}/retry-complete")]
    public IActionResult RetryComplete(string orderId)
        => StatusCode(StatusCodes.Status410Gone, ApiResponse<object>.Fail(RetryRetiredCode,
            "Retrying a signup is no longer available: it activated without charging. Confirm the crypto " +
            "payment, use Re-check for an SBP (Entrax) payment, or activate the member manually from their status."));

    public record PendingSignupDto(
        string MemberId,
        string FullName,
        string Email,
        string Country,
        string? SponsorMemberId,
        DateTime EnrollDate,
        string OrderId,
        decimal OrderTotal);
}
