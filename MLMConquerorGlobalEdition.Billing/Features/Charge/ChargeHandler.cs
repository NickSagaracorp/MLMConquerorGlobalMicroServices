using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Charge;

public class ChargeHandler : IRequestHandler<ChargeCommand, Result<ChargeResponse>>
{
    private readonly AppDbContext _db;
    private readonly IGatewayResolver _gatewayResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public ChargeHandler(
        AppDbContext db,
        IGatewayResolver gatewayResolver,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _gatewayResolver = gatewayResolver;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<ChargeResponse>> Handle(ChargeCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // Validate member exists and is active
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == req.MemberId && !m.IsDeleted, ct);

        if (member is null)
            return Result<ChargeResponse>.Failure("MEMBER_NOT_FOUND", $"Member '{req.MemberId}' not found.");

        if (member.Status != MemberAccountStatus.Active)
            return Result<ChargeResponse>.Failure("MEMBER_INACTIVE",
                $"Member '{req.MemberId}' is not active. Current status: {member.Status}.");

        // Resolve gateway
        IGatewayService gateway;
        try
        {
            gateway = _gatewayResolver.Resolve(req.Gateway);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ChargeResponse>.Failure("GATEWAY_NOT_SUPPORTED", ex.Message);
        }

        // Determine or create order
        string orderId;
        if (!string.IsNullOrWhiteSpace(req.OrderId))
        {
            var existingOrder = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == req.OrderId && !o.IsDeleted, ct);
            if (existingOrder is null)
                return Result<ChargeResponse>.Failure("ORDER_NOT_FOUND", $"Order '{req.OrderId}' not found.");
            orderId = existingOrder.Id;
        }
        else
        {
            var now = _dateTime.Now;
            string orderNo;
            do { orderNo = OrderNumberHelper.Generate(req.Description ?? "Order", now); }
            while (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct));

            var newOrder = new Orders
            {
                MemberId = req.MemberId,
                OrderNo = orderNo,
                TotalAmount = req.Amount,
                Status = OrderStatus.Processing,
                OrderDate = now,
                Notes = req.Description,
                CreationDate = now,
                CreatedBy = _currentUser.UserId,
                LastUpdateDate = now,
                LastUpdateBy = _currentUser.UserId
            };
            _db.Orders.Add(newOrder);
            orderId = newOrder.Id;
        }

        // Call gateway
        var chargeResult = await gateway.ChargeAsync(req.MemberId, req.Amount, req.Currency, req.Description, ct);
        if (!chargeResult.IsSuccess)
            return Result<ChargeResponse>.Failure(chargeResult.ErrorCode!, chargeResult.Error!);

        var gatewayTransactionId = chargeResult.Value!;
        var Now = _dateTime.Now;

        // Create PaymentHistory record
        var payment = new PaymentHistory
        {
            OrderId = orderId,
            MemberId = req.MemberId,
            Amount = req.Amount,
            GatewayName = req.Gateway.ToString(),
            GatewayTransactionId = gatewayTransactionId,
            TransactionStatus = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt = Now,
            CreationDate = Now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = Now,
            LastUpdateBy = _currentUser.UserId
        };

        _db.PaymentHistories.Add(payment);
        await _db.SaveChangesAsync(ct);

        return Result<ChargeResponse>.Success(new ChargeResponse
        {
            PaymentHistoryId = payment.Id,
            GatewayTransactionId = gatewayTransactionId,
            Amount = req.Amount,
            Gateway = req.Gateway.ToString(),
            Status = payment.TransactionStatus.ToString()
        });
    }
}
