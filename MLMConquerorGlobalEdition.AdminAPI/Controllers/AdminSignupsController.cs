using System.Net.Http.Json;
using System.Text.Json;
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
    private readonly AppDbContext       _db;
    private readonly IHttpClientFactory _httpFactory;

    public AdminSignupsController(AppDbContext db, IHttpClientFactory httpFactory)
    {
        _db          = db;
        _httpFactory = httpFactory;
    }

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
    /// POST /api/v1/admin/signups/{orderId}/retry-complete —
    /// re-runs the Complete-Signup flow for a stuck pending signup. Forwards to
    /// SignupAPI's existing /complete endpoint with a no-card payload, since the
    /// original card data was lost when the prior attempt crashed.
    /// </summary>
    [HttpPost("{orderId}/retry-complete")]
    public async Task<IActionResult> RetryComplete(string orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.Status == OrderStatus.Pending, ct);
        if (order is null)
            return NotFound(ApiResponse<object>.Fail("ORDER_NOT_FOUND",
                "No pending order found for that id."));

        var http = _httpFactory.CreateClient("SignupApi");
        // PaymentMethod = 1 = CreditCard. We pass no card body so the handler
        // skips the credit-card insert (and just flips statuses to Active).
        var payload = new { paymentMethod = 1 };
        var resp = await http.PostAsJsonAsync($"api/v1/signups/{orderId}/complete", payload, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        return new ContentResult
        {
            Content     = body,
            ContentType = "application/json",
            StatusCode  = (int)resp.StatusCode
        };
    }

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
