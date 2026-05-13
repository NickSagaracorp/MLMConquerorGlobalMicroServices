using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Base class for stub gateway implementations.
/// Reads its ApiCredential by ServiceKey, validates it is present and active,
/// then returns a simulated success/failure result.
/// Real HTTP/SDK wiring to be added per gateway when credentials are available.
/// </summary>
public abstract class StubGatewayBase : ICardGatewayService
{
    protected readonly AppDbContext _db;
    protected readonly ILogger      _logger;

    protected StubGatewayBase(AppDbContext db, ILogger logger)
    {
        _db     = db;
        _logger = logger;
    }

    public abstract CardProcessor Processor { get; }

    protected abstract string CredentialKey { get; }

    public async Task<Result<GatewayChargeResult>> ChargeAsync(
        GatewayChargeRequest req,
        CancellationToken ct = default)
    {
        var credResult = await ValidateCredentialAsync(ct);
        if (!credResult.IsSuccess)
            return Result<GatewayChargeResult>.Failure(credResult.ErrorCode!, credResult.Error!);

        _logger.LogInformation(
            "[STUB] {Processor}: charging {Amount} {Currency} for member {MemberId}.",
            Processor, req.Amount, req.Currency, req.MemberId);

        // Simulated success — replace with real SDK call when credentials land
        return Result<GatewayChargeResult>.Success(new GatewayChargeResult
        {
            GatewayTransactionId = $"STUB-{Processor}-{Guid.NewGuid():N}",
            Status               = "simulated_success",
            RawResponse          = $"{{\"stub\":true,\"processor\":\"{Processor}\"}}"
        });
    }

    public async Task<Result<bool>> RefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default)
    {
        var credResult = await ValidateCredentialAsync(ct);
        if (!credResult.IsSuccess)
            return Result<bool>.Failure(credResult.ErrorCode!, credResult.Error!);

        _logger.LogInformation(
            "[STUB] {Processor}: refunding {Amount} for transaction {TxId}.",
            Processor, amount, gatewayTransactionId);

        return Result<bool>.Success(true);
    }

    private async Task<Result<bool>> ValidateCredentialAsync(CancellationToken ct)
    {
        var cred = await _db.ApiCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ServiceKey == CredentialKey && !c.IsDeleted, ct);

        if (cred is null)
            return Result<bool>.Failure(
                "CREDENTIAL_NOT_FOUND",
                $"{Processor}: ApiCredential '{CredentialKey}' not found. Please configure it via the Admin API.");

        if (!cred.IsActive)
            return Result<bool>.Failure(
                "CREDENTIAL_INACTIVE",
                $"{Processor}: ApiCredential '{CredentialKey}' is inactive.");

        if (string.IsNullOrWhiteSpace(cred.ApiKeyEncrypted))
            return Result<bool>.Failure(
                "CREDENTIAL_INCOMPLETE",
                $"{Processor}: ApiKeyEncrypted is not set for '{CredentialKey}'.");

        return Result<bool>.Success(true);
    }
}
