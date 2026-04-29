using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Wallet;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Repository.Services.Wallets;

/// <inheritdoc />
public class MemberWalletService : IMemberWalletService
{
    private readonly AppDbContext _db;

    public MemberWalletService(AppDbContext db) => _db = db;

    public async Task<List<WalletAccountView>> GetAccountsAsync(string memberId, CancellationToken ct = default)
    {
        var rows = await _db.Wallets.AsNoTracking()
            .Where(w => w.MemberId == memberId && !w.IsDeleted)
            .OrderByDescending(w => w.IsPreferred).ThenBy(w => w.WalletType)
            .ToListAsync(ct);
        return rows.Select(MapToView).ToList();
    }

    public async Task<WalletAccountView?> GetDefaultAsync(string memberId, CancellationToken ct = default)
    {
        var row = await _db.Wallets.AsNoTracking()
            .Where(w => w.MemberId == memberId && !w.IsDeleted && w.IsPreferred)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : MapToView(row);
    }

    public async Task<Result<WalletAccountView>> SaveAccountAsync(
        string memberId, SaveWalletRequest request, string actorIdentifier,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccountIdentifier))
            return Result<WalletAccountView>.Failure(
                "ACCOUNT_IDENTIFIER_REQUIRED",
                "An account identifier (email / wallet address / username) is required.");

        var trimmed = request.AccountIdentifier.Trim();
        var validation = ValidateAccountIdentifier(request.WalletType, trimmed);
        if (validation is not null)
            return Result<WalletAccountView>.Failure("INVALID_ACCOUNT_FORMAT", validation);
        request.AccountIdentifier = trimmed;

        var existing = await _db.Wallets
            .FirstOrDefaultAsync(w => w.MemberId == memberId
                                   && w.WalletType == request.WalletType
                                   && !w.IsDeleted, ct);

        var now = DateTime.UtcNow;
        var apiResult = await CallGatewayRegisterAsync(memberId, request, ct);
        await _db.WalletApiLogs.AddAsync(apiResult.Log, ct);

        if (existing is null)
        {
            // First time the member sets up this gateway type.
            var newWallet = new MemberProfilesWallet
            {
                MemberId          = memberId,
                WalletType        = request.WalletType,
                Status            = apiResult.NewStatus,
                AccountIdentifier = request.AccountIdentifier,
                Notes             = request.Notes,
                IsPreferred       = false,
                CreationDate      = now,
                CreatedBy         = actorIdentifier,
                LastUpdateDate    = now,
                LastUpdateBy      = actorIdentifier
            };
            if (request.WalletType == WalletType.eWallet
                && !string.IsNullOrEmpty(request.EWalletPasswordEncrypted))
                newWallet.SetEWalletPassword(request.EWalletPasswordEncrypted);

            await _db.Wallets.AddAsync(newWallet, ct);
            await AddHistoryAsync(newWallet, WalletHistoryAction.Created,
                oldStatus: WalletStatus.Pending, newStatus: apiResult.NewStatus,
                oldAccount: null, newAccount: request.AccountIdentifier,
                oldDefault: null, newDefault: false,
                actor: actorIdentifier, reason: $"Initial registration via {request.WalletType}.", ct);

            await _db.SaveChangesAsync(ct);
            return Result<WalletAccountView>.Success(MapToView(newWallet));
        }

        // Update path — capture before-image for history, then mutate.
        var oldStatus  = existing.Status;
        var oldAccount = existing.AccountIdentifier;
        var oldNotes   = existing.Notes;

        existing.AccountIdentifier = request.AccountIdentifier;
        existing.Notes             = request.Notes;
        existing.Status            = apiResult.NewStatus;
        existing.LastUpdateDate    = now;
        existing.LastUpdateBy      = actorIdentifier;
        if (request.WalletType == WalletType.eWallet
            && !string.IsNullOrEmpty(request.EWalletPasswordEncrypted))
            existing.SetEWalletPassword(request.EWalletPasswordEncrypted);

        if (!string.Equals(oldAccount, request.AccountIdentifier, StringComparison.Ordinal))
            await AddHistoryAsync(existing, WalletHistoryAction.AccountChanged,
                oldStatus: oldStatus, newStatus: existing.Status,
                oldAccount: oldAccount, newAccount: request.AccountIdentifier,
                oldDefault: null, newDefault: null,
                actor: actorIdentifier,
                reason: "Account identifier changed by member.", ct);

        if (oldStatus != existing.Status)
            await AddHistoryAsync(existing, WalletHistoryAction.StatusChanged,
                oldStatus: oldStatus, newStatus: existing.Status,
                oldAccount: oldAccount, newAccount: existing.AccountIdentifier,
                oldDefault: null, newDefault: null,
                actor: actorIdentifier,
                reason: $"Status changed after gateway response (HTTP {apiResult.Log.HttpStatusCode}).", ct);

        await _db.SaveChangesAsync(ct);
        return Result<WalletAccountView>.Success(MapToView(existing));
    }

    public async Task<Result<WalletAccountView>> SetDefaultAsync(
        string memberId, WalletType walletType, string actorIdentifier,
        CancellationToken ct = default)
    {
        var target = await _db.Wallets
            .FirstOrDefaultAsync(w => w.MemberId == memberId
                                   && w.WalletType == walletType
                                   && !w.IsDeleted, ct);
        if (target is null)
            return Result<WalletAccountView>.Failure(
                "WALLET_NOT_FOUND",
                "No wallet of that type exists for this member yet.");

        if (target.Status != WalletStatus.Approved)
            return Result<WalletAccountView>.Failure(
                "WALLET_NOT_APPROVED",
                "Only approved wallets can be set as default.");

        var now    = DateTime.UtcNow;
        var others = await _db.Wallets
            .Where(w => w.MemberId == memberId && w.IsPreferred && !w.IsDeleted && w.Id != target.Id)
            .ToListAsync(ct);

        foreach (var other in others)
        {
            await AddHistoryAsync(other, WalletHistoryAction.UnsetAsDefault,
                oldStatus: other.Status, newStatus: other.Status,
                oldAccount: other.AccountIdentifier, newAccount: other.AccountIdentifier,
                oldDefault: true, newDefault: false,
                actor: actorIdentifier,
                reason: $"Replaced as default by {walletType}.", ct);
            other.IsPreferred    = false;
            other.LastUpdateDate = now;
            other.LastUpdateBy   = actorIdentifier;
        }

        if (!target.IsPreferred)
            await AddHistoryAsync(target, WalletHistoryAction.SetAsDefault,
                oldStatus: target.Status, newStatus: target.Status,
                oldAccount: target.AccountIdentifier, newAccount: target.AccountIdentifier,
                oldDefault: false, newDefault: true,
                actor: actorIdentifier,
                reason: "Member set this wallet as default.", ct);

        target.IsPreferred    = true;
        target.LastUpdateDate = now;
        target.LastUpdateBy   = actorIdentifier;

        await _db.SaveChangesAsync(ct);
        return Result<WalletAccountView>.Success(MapToView(target));
    }

    public async Task<PagedResult<WalletHistoryView>> GetHistoryAsync(
        string memberId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = _db.WalletHistories.AsNoTracking()
            .Where(h => h.MemberId == memberId)
            .OrderByDescending(h => h.CreationDate);

        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<WalletHistoryView>
        {
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            Items      = rows.Select(h => new WalletHistoryView
            {
                Id                   = h.Id,
                WalletId             = h.WalletId,
                WalletType           = h.WalletType,
                Action               = h.Action.ToString(),
                OldStatus            = h.OldStatus,
                NewStatus            = h.NewStatus,
                OldAccountIdentifier = h.OldAccountIdentifier,
                NewAccountIdentifier = h.NewAccountIdentifier,
                OldIsPreferred       = h.OldIsPreferred,
                NewIsPreferred       = h.NewIsPreferred,
                ChangeReason         = h.ChangeReason,
                ChangedBy            = h.CreatedBy,
                OccurredAt           = h.CreationDate
            }).ToList()
        };
    }

    public async Task<PagedResult<WalletApiLogView>> GetApiLogsAsync(
        string memberId, WalletType? walletType = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var query = _db.WalletApiLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId);
        if (walletType.HasValue)
            query = query.Where(l => l.WalletType == walletType.Value);

        var ordered = query.OrderByDescending(l => l.CreationDate);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<WalletApiLogView>
        {
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            Items      = rows.Select(l => new WalletApiLogView
            {
                Id             = l.Id,
                WalletType     = l.WalletType,
                Operation      = l.Operation,
                Endpoint       = l.Endpoint,
                HttpMethod     = l.HttpMethod,
                RequestBody    = l.RequestBody,
                HttpStatusCode = l.HttpStatusCode,
                ResponseBody   = l.ResponseBody,
                Success        = l.Success,
                ErrorMessage   = l.ErrorMessage,
                DurationMs     = l.DurationMs,
                OccurredAt     = l.CreationDate
            }).ToList()
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private static WalletAccountView MapToView(MemberProfilesWallet w) => new()
    {
        WalletId          = w.Id,
        MemberId          = w.MemberId,
        WalletType        = w.WalletType,
        WalletTypeName    = w.WalletType.ToString(),
        Status            = w.Status,
        StatusName        = w.Status.ToString(),
        AccountIdentifier = w.AccountIdentifier,
        IsPreferred       = w.IsPreferred,
        Notes             = w.Notes,
        CreationDate      = w.CreationDate,
        LastUpdateDate    = w.LastUpdateDate
    };

    private async Task AddHistoryAsync(
        MemberProfilesWallet wallet, WalletHistoryAction action,
        WalletStatus oldStatus, WalletStatus newStatus,
        string? oldAccount, string? newAccount,
        bool? oldDefault, bool? newDefault,
        string actor, string reason, CancellationToken ct)
    {
        await _db.WalletHistories.AddAsync(new MemberProfilesWalletHistory
        {
            WalletId             = wallet.Id,
            MemberId             = wallet.MemberId,
            WalletType           = wallet.WalletType,
            Action               = action,
            OldStatus            = oldStatus,
            NewStatus            = newStatus,
            OldAccountIdentifier = oldAccount,
            NewAccountIdentifier = newAccount,
            OldIsPreferred       = oldDefault,
            NewIsPreferred       = newDefault,
            ChangeReason         = reason,
            CreatedBy            = actor,
            CreationDate         = DateTime.UtcNow
        }, ct);
    }

    public async Task<List<PaymentGatewayInfoView>> GetGatewayInfoAsync(CancellationToken ct = default)
    {
        var rows = await _db.PaymentGateways.AsNoTracking()
            .Where(g => g.IsActive)
            .OrderBy(g => g.WalletType)
            .ToListAsync(ct);

        return rows.Select(g => new PaymentGatewayInfoView
        {
            WalletType      = g.WalletType,
            WalletTypeName  = g.WalletType.ToString(),
            DisplayName     = g.DisplayName,
            Description     = g.Description,
            AdminFee        = g.AdminFee,
            AdminFeeKind    = g.AdminFeeKind.ToString(),
            MinAdminFee     = g.MinAdminFee,
            AdminFeeDisplay = FormatAdminFee(g),
            Currency        = g.Currency,
            IsActive        = g.IsActive
        }).ToList();
    }

    private static string FormatAdminFee(PaymentGatewayInfo g) =>
        g.AdminFeeKind switch
        {
            AdminFeeKind.Percentage when g.MinAdminFee is decimal min
                => $"{g.AdminFee:0.##}% (min {min:0.00} {g.Currency})",
            AdminFeeKind.Percentage
                => $"{g.AdminFee:0.##}%",
            _   => $"{g.AdminFee:0.00} {g.Currency}"
        };

    // ── Account-identifier format validation ───────────────────────────────
    // Crypto: BTC (bc1.../1.../3...) or ETH/EVM (0x + 40 hex) — explicitly NOT an email.
    // Email-based gateways: must be a valid email shape.

    private static readonly Regex EmailPattern =
        new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

    private static readonly Regex BtcLegacyOrSegwitPattern =
        new(@"^[13][a-km-zA-HJ-NP-Z1-9]{25,34}$", RegexOptions.Compiled);

    private static readonly Regex BtcBech32Pattern =
        new(@"^(bc1)[a-z0-9]{6,87}$", RegexOptions.Compiled);

    private static readonly Regex EvmAddressPattern =
        new(@"^0x[a-fA-F0-9]{40}$", RegexOptions.Compiled);

    private static readonly Regex TronAddressPattern =
        new(@"^T[1-9A-HJ-NP-Za-km-z]{33}$", RegexOptions.Compiled);

    private static string? ValidateAccountIdentifier(WalletType type, string value)
    {
        if (type == WalletType.Crypto)
        {
            if (EmailPattern.IsMatch(value))
                return "A crypto wallet address is required, not an email. " +
                       "Provide a Bitcoin (bc1.../1.../3...), USDT-TRC20 (T...), or EVM (0x...) address.";

            var looksLikeCrypto =
                BtcBech32Pattern.IsMatch(value) ||
                BtcLegacyOrSegwitPattern.IsMatch(value) ||
                EvmAddressPattern.IsMatch(value) ||
                TronAddressPattern.IsMatch(value);

            if (!looksLikeCrypto)
                return "Account does not look like a valid wallet address. " +
                       "Supported formats: BTC (bc1.../1.../3...), USDT-TRC20 (T...), or EVM/ERC-20 (0x followed by 40 hex chars).";

            return null;
        }

        // All other gateways (eWallet/I-Payout, Dwolla, Advancash) are email-based.
        if (!EmailPattern.IsMatch(value))
            return $"{type} expects an email address as the account identifier.";

        return null;
    }

    /// <summary>
    /// Stub implementation for the gateway-registration HTTP call. Real integrations
    /// (I-Payout, Dwolla, Crypto, Advancash) will replace the body of this method
    /// with HttpClient calls to the gateway endpoints. Today it ALWAYS records a
    /// realistic-looking request/response in MemberWalletApiLog and returns "Approved"
    /// for known-good fixtures so the audit-trail pipeline is exercised end-to-end.
    /// </summary>
    private static async Task<(MemberWalletApiLog Log, WalletStatus NewStatus)> CallGatewayRegisterAsync(
        string memberId, SaveWalletRequest req, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var endpoint = req.WalletType switch
        {
            WalletType.eWallet   => "https://api.i-payout.com/v1/accounts/register",
            WalletType.Dwolla    => "https://api.dwolla.com/customers",
            WalletType.Crypto    => "https://api.coinbase.com/v2/accounts",
            WalletType.Advancash => "https://api.advcash.com/v1/wallets/register",
            _                    => $"https://api.{req.WalletType.ToString().ToLowerInvariant()}.example/register"
        };

        var requestBody = JsonSerializer.Serialize(new
        {
            memberId,
            walletType = req.WalletType.ToString(),
            accountIdentifier = req.AccountIdentifier
        });

        // Simulate a gateway round-trip until real integrations are wired in.
        await Task.Delay(20, ct);

        var success = !string.IsNullOrWhiteSpace(req.AccountIdentifier);
        var responseBody = success
            ? JsonSerializer.Serialize(new { status = "approved", accountReference = Guid.NewGuid().ToString() })
            : JsonSerializer.Serialize(new { status = "rejected", reason = "Missing account identifier" });

        sw.Stop();

        var log = new MemberWalletApiLog
        {
            MemberId       = memberId,
            WalletType     = req.WalletType,
            Operation      = "RegisterAccount",
            Endpoint       = endpoint,
            HttpMethod     = "POST",
            RequestBody    = requestBody,
            HttpStatusCode = success ? 200 : 400,
            ResponseBody   = responseBody,
            Success        = success,
            ErrorMessage   = success ? null : "Missing account identifier",
            DurationMs     = (int)sw.ElapsedMilliseconds,
            CreationDate   = DateTime.UtcNow,
            CreatedBy      = "system"
        };

        return (log, success ? WalletStatus.Approved : WalletStatus.Pending);
    }
}
