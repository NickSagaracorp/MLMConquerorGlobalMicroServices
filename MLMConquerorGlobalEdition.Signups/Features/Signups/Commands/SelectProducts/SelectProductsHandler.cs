using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SelectProducts;

/// <summary>
/// Phase 2 of the signup wizard — add or replace the product selection on a pending order.
/// </summary>
public class SelectProductsHandler : IRequestHandler<SelectProductsCommand, Result<bool>>
{
    private readonly AppDbContext      _db;
    private readonly IDateTimeProvider _dateTime;

    public SelectProductsHandler(AppDbContext db, IDateTimeProvider dateTime)
    {
        _db       = db;
        _dateTime = dateTime;
    }

    public async Task<Result<bool>> Handle(SelectProductsCommand command, CancellationToken ct)
    {
        // ── Locate pending order ──────────────────────────────────────────────
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == command.SignupId && o.Status == OrderStatus.Pending, ct);

        if (order is null)
            return Result<bool>.Failure("SIGNUP_NOT_FOUND", "Pending signup not found.");

        // ── Load the member for audit context ─────────────────────────────────
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == order.MemberId, ct);

        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", "Associated member not found.");

        // ── Validate products ─────────────────────────────────────────────────
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => command.Request.ProductIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(ct);

        if (products.Count == 0)
            return Result<bool>.Failure("NO_VALID_PRODUCTS", "None of the selected products are valid or active.");

        var now = _dateTime.Now;

        // ── Replace existing order details ────────────────────────────────────
        var existingDetails = await _db.OrderDetails
            .Where(d => d.OrderId == order.Id)
            .ToListAsync(ct);
        _db.OrderDetails.RemoveRange(existingDetails);

        var newDetails = products.Select(p => new OrderDetail
        {
            OrderId      = order.Id,
            ProductId    = p.Id,
            Quantity     = 1,
            UnitPrice    = p.SetupFee + p.MonthlyFee,
            CreatedBy    = member.Email,
            CreationDate = now
        }).ToList();

        await _db.OrderDetails.AddRangeAsync(newDetails, ct);

        // ── Update order total ────────────────────────────────────────────────
        order.TotalAmount    = products.Sum(p => p.SetupFee + p.MonthlyFee);
        order.LastUpdateDate = now;
        order.LastUpdateBy   = member.Email;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
