using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Billing.DTOs;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.CardGateway;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Orders;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Features.Charge;

public class ChargeHandler : IRequestHandler<ChargeCommand, Result<ChargeResponse>>
{
    private readonly AppDbContext                   _db;
    private readonly IGatewayResolver               _legacyGatewayResolver;
    private readonly IGatewayRouter                 _router;
    private readonly IGatewayChargeOrchestrator     _orchestrator;
    private readonly ICardBrandDetector             _brandDetector;
    private readonly ICurrentUserService            _currentUser;
    private readonly IDateTimeProvider              _dateTime;

    public ChargeHandler(
        AppDbContext db,
        IGatewayResolver legacyGatewayResolver,
        IGatewayRouter router,
        IGatewayChargeOrchestrator orchestrator,
        ICardBrandDetector brandDetector,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db                    = db;
        _legacyGatewayResolver = legacyGatewayResolver;
        _router                = router;
        _orchestrator          = orchestrator;
        _brandDetector         = brandDetector;
        _currentUser           = currentUser;
        _dateTime              = dateTime;
    }

    public async Task<Result<ChargeResponse>> Handle(ChargeCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // Validate member
        var member = await _db.MemberProfiles
            .FirstOrDefaultAsync(m => m.MemberId == req.MemberId && !m.IsDeleted, ct);

        if (member is null)
            return Result<ChargeResponse>.Failure("MEMBER_NOT_FOUND", $"Member '{req.MemberId}' not found.");

        if (member.Status != MemberAccountStatus.Active)
            return Result<ChargeResponse>.Failure("MEMBER_INACTIVE",
                $"Member '{req.MemberId}' is not active. Current status: {member.Status}.");

        // ── Legacy wallet/payout path (when Gateway WalletType is provided) ─
        if (req.Gateway.HasValue && req.ProcessorOverride is null)
        {
            return await HandleLegacyChargeAsync(req, ct);
        }

        // ── New card routing path ──────────────────────────────────────────

        // Detect card brand if not supplied
        var cardBrand = req.CardBrand ?? (string.IsNullOrWhiteSpace(req.CardFirst6)
            ? CardBrand.Other
            : _brandDetector.Detect(req.CardFirst6));

        // Build routing context
        var routingCtx = new GatewayRoutingContext
        {
            OperationType        = req.OperationType,
            CardBrand            = cardBrand,
            CardholderCountryIso = req.CardholderCountryIso,
            AmountUsd            = req.Amount,
            MemberId             = req.MemberId,
            AdminOverride        = req.ProcessorOverride
        };

        // Resolve routing plan
        var planResult = await _router.ResolveAsync(routingCtx, ct);
        if (!planResult.IsSuccess)
            return Result<ChargeResponse>.Failure(planResult.ErrorCode!, planResult.Error!);

        // Determine or create order
        string orderId = await EnsureOrderAsync(req, ct);

        // Execute charge plan
        var chargeReq = new OrchestratorChargeRequest
        {
            MemberId             = req.MemberId,
            TokenizedCardRef     = req.TokenizedCardRef,
            NetworkTransactionId = req.NetworkTransactionId,
            Description          = req.Description,
            OrderId              = orderId,
            IsRecurring          = false
        };

        var result = await _orchestrator.ExecuteAsync(planResult.Value!, routingCtx, chargeReq, ct);
        if (!result.IsSuccess)
            return Result<ChargeResponse>.Failure(result.ErrorCode!, result.Error!);

        var outcome = result.Value!;
        return Result<ChargeResponse>.Success(new ChargeResponse
        {
            PaymentHistoryId     = outcome.PaymentHistoryId,
            GatewayTransactionId = outcome.GatewayTransactionId,
            Amount               = outcome.AmountCharged,
            Gateway              = outcome.ProcessorUsed,
            Status               = outcome.Status
        });
    }

    // ── Legacy path (wallet/payout gateway) ───────────────────────────────

    private async Task<Result<ChargeResponse>> HandleLegacyChargeAsync(
        ChargeRequest req, CancellationToken ct)
    {
        IGatewayService gateway;
        try
        {
            gateway = _legacyGatewayResolver.Resolve(req.Gateway!.Value);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ChargeResponse>.Failure("GATEWAY_NOT_SUPPORTED", ex.Message);
        }

        string orderId = await EnsureOrderAsync(req, ct);

        var chargeResult = await gateway.ChargeAsync(
            req.MemberId, req.Amount, req.Currency, req.Description, ct);

        if (!chargeResult.IsSuccess)
            return Result<ChargeResponse>.Failure(chargeResult.ErrorCode!, chargeResult.Error!);

        var now = _dateTime.Now;
        var payment = new PaymentHistory
        {
            OrderId              = orderId,
            MemberId             = req.MemberId,
            Amount               = req.Amount,
            GatewayName          = req.Gateway!.Value.ToString(),
            GatewayTransactionId = chargeResult.Value!,
            TransactionStatus    = PaymentHistoryTransactionStatus.Captured,
            ProcessedAt          = now,
            CreationDate         = now,
            CreatedBy            = _currentUser.UserId,
            LastUpdateDate       = now,
            LastUpdateBy         = _currentUser.UserId
        };

        _db.PaymentHistories.Add(payment);
        await _db.SaveChangesAsync(ct);

        return Result<ChargeResponse>.Success(new ChargeResponse
        {
            PaymentHistoryId     = payment.Id,
            GatewayTransactionId = chargeResult.Value!,
            Amount               = req.Amount,
            Gateway              = req.Gateway!.Value.ToString(),
            Status               = payment.TransactionStatus.ToString()
        });
    }

    private async Task<string> EnsureOrderAsync(ChargeRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.OrderId))
            return req.OrderId;

        var now = _dateTime.Now;
        string orderNo;
        do { orderNo = OrderNumberHelper.Generate(req.Description ?? "Order", now); }
        while (await _db.Orders.AnyAsync(o => o.OrderNo == orderNo, ct));

        var newOrder = new Orders
        {
            MemberId       = req.MemberId,
            OrderNo        = orderNo,
            TotalAmount    = req.Amount,
            Status         = OrderStatus.Processing,
            OrderDate      = now,
            Notes          = req.Description,
            CreationDate   = now,
            CreatedBy      = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy   = _currentUser.UserId
        };
        _db.Orders.Add(newOrder);
        return newOrder.Id;
    }
}
