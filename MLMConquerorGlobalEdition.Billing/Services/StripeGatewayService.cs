using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.SharedKernel;
using Stripe;

namespace MLMConquerorGlobalEdition.Billing.Services;

public class StripeGatewayService : IGatewayService
{
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;

    public WalletType GatewayType => WalletType.Dwolla;

    public StripeGatewayService(IConfiguration configuration)
    {
        var secretKey = configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");

        StripeConfiguration.ApiKey = secretKey;
        _paymentIntentService = new PaymentIntentService();
        _refundService = new RefundService();
    }

    public async Task<Result<string>> ChargeAsync(
        string memberId,
        decimal amount,
        string currency,
        string description,
        CancellationToken ct = default)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = currency.ToLowerInvariant(),
                Description = description,
                Metadata = new Dictionary<string, string>
                {
                    { "memberId", memberId }
                },
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
            };

            var paymentIntent = await _paymentIntentService.CreateAsync(options, cancellationToken: ct);
            return Result<string>.Success(paymentIntent.Id);
        }
        catch (StripeException ex)
        {
            return Result<string>.Failure("STRIPE_CHARGE_FAILED", ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("STRIPE_CHARGE_FAILED", ex.Message);
        }
    }

    public async Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default)
    {
        try
        {
            var options = new RefundCreateOptions
            {
                PaymentIntent = gatewayTransactionId,
                Amount = (long)(amount * 100)
            };

            var refund = await _refundService.CreateAsync(options, cancellationToken: ct);
            return Result<bool>.Success(refund.Status == "succeeded" || refund.Status == "pending");
        }
        catch (StripeException ex)
        {
            return Result<bool>.Failure("STRIPE_REFUND_FAILED", ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure("STRIPE_REFUND_FAILED", ex.Message);
        }
    }
}
