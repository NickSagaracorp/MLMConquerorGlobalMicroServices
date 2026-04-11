using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SelectProducts;

/// <summary>
/// Phase 2 of the signup wizard — add or replace the product selection on a pending order.
/// Validates that selected products are allowed for the member's country when mappings exist.
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
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == command.SignupId && o.Status == OrderStatus.Pending, ct);

        if (order is null)
            return Result<bool>.Failure("SIGNUP_NOT_FOUND", "Pending signup not found.");

        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == order.MemberId, ct);

        if (member is null)
            return Result<bool>.Failure("MEMBER_NOT_FOUND", "Associated member not found.");

        // Resolve country-product restrictions (Iso2 stored in MemberProfile.Country)
        var iso2 = member.Country?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(iso2))
        {
            var countryId = await _db.Countries
                .AsNoTracking()
                .Where(c => c.Iso2 == iso2)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);

            if (countryId is not null)
            {
                var allowedProductIds = await _db.CountryProducts
                    .AsNoTracking()
                    .Where(cp => cp.CountryId == countryId && cp.IsActive)
                    .Select(cp => cp.ProductId)
                    .ToListAsync(ct);

                if (allowedProductIds.Count > 0)
                {
                    var disallowed = command.Request.ProductIds
                        .Where(id => !allowedProductIds.Contains(id))
                        .ToList();

                    if (disallowed.Count > 0)
                        return Result<bool>.Failure(
                            "PRODUCT_NOT_ALLOWED_IN_COUNTRY",
                            $"The following products are not available for your country: {string.Join(", ", disallowed)}.");
                }
            }
        }

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => command.Request.ProductIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(ct);

        if (products.Count == 0)
            return Result<bool>.Failure("NO_VALID_PRODUCTS", "None of the selected products are valid or active.");

        var now = _dateTime.Now;

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

        order.TotalAmount    = products.Sum(p => p.SetupFee + p.MonthlyFee);
        order.LastUpdateDate = now;
        order.LastUpdateBy   = member.Email;

        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
