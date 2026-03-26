using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Payments;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Payments.GetAdminPayments;

public class GetAdminPaymentsHandler : IRequestHandler<GetAdminPaymentsQuery, Result<PagedResult<AdminPaymentDto>>>
{
    private readonly AppDbContext _db;

    public GetAdminPaymentsHandler(AppDbContext db) => _db = db;

    public async Task<Result<PagedResult<AdminPaymentDto>>> Handle(
        GetAdminPaymentsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.PaymentHistories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
            query = query.Where(p => p.TransactionStatus.ToString() == request.StatusFilter);

        if (!string.IsNullOrWhiteSpace(request.MemberId))
            query = query.Where(p => p.MemberId == request.MemberId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreationDate)
            .Skip((request.Page.Page - 1) * request.Page.PageSize)
            .Take(request.Page.PageSize)
            .Select(p => new AdminPaymentDto
            {
                Id = p.Id,
                MemberId = p.MemberId,
                OrderId = p.OrderId,
                Amount = p.Amount,
                GatewayName = p.GatewayName,
                GatewayTransactionId = p.GatewayTransactionId,
                Status = p.TransactionStatus.ToString(),
                FailureReason = p.FailureReason,
                ProcessedAt = p.ProcessedAt,
                CreationDate = p.CreationDate
            })
            .ToListAsync(cancellationToken);

        return Result<PagedResult<AdminPaymentDto>>.Success(new PagedResult<AdminPaymentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page.Page,
            PageSize = request.Page.PageSize
        });
    }
}
