using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Exceptions;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Tokens.ValidateToken;

/// <summary>
/// Real-time token validation for the join page.
///
/// Always succeeds at the HTTP layer (Result.Success) so the response carries a structured
/// <see cref="ValidateTokenResponse"/>. The booleans + messages inside the response indicate
/// whether the token is usable; an exception is only raised on infrastructure faults.
///
/// Generic-message exceptions all collapse to "TOKEN_NOT_VALID" — the caller cannot distinguish
/// not-found / already-used / wrong-sponsor / expired. The product-mismatch case carries a
/// specific message since the user already knows their own selection.
/// </summary>
public class ValidateTokenHandler : IRequestHandler<ValidateTokenQuery, Result<ValidateTokenResponse>>
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _dateTime;
    private readonly ILogger<ValidateTokenHandler> _logger;

    private const string GenericMessage = "This token is not valid for this signup.";

    public ValidateTokenHandler(
        AppDbContext db,
        IDateTimeProvider dateTime,
        ILogger<ValidateTokenHandler> logger)
    {
        _db = db;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task<Result<ValidateTokenResponse>> Handle(ValidateTokenQuery query, CancellationToken ct)
    {
        var req  = query.Request;
        var code = req.Code.Trim().ToUpperInvariant();
        var now  = _dateTime.Now;

        // Resolve sponsor by replicate-site slug or by MemberId — matches ValidateSponsor logic.
        var sponsor = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => m.MemberId == req.SponsorReplicateSite ||
                        m.ReplicateSiteSlug == req.SponsorReplicateSite)
            .Select(m => new { m.MemberId })
            .FirstOrDefaultAsync(ct);

        if (sponsor is null)
        {
            _logger.LogInformation("ValidateToken: sponsor '{Sponsor}' not found", req.SponsorReplicateSite);
            return Generic();
        }

        // Single redeemable instance row.
        var instance = await _db.TokenTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferenceId == code, ct);

        if (instance is null)
        {
            _logger.LogInformation("ValidateToken: code '{Code}' not found", code);
            return Generic();
        }

        var tokenType = await _db.TokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == instance.TokenTypeId, ct);

        if (tokenType is null)
        {
            _logger.LogWarning("ValidateToken: token code '{Code}' references missing TokenTypeId={TypeId}",
                code, instance.TokenTypeId);
            return Generic();
        }

        var grantedLinks = await _db.TokenTypeProducts
            .AsNoTracking()
            .Where(p => p.TokenTypeId == tokenType.Id)
            .ToListAsync(ct);

        try
        {
            TokenInstanceSignupValidator.Validate(
                instance,
                tokenType,
                grantedLinks,
                sponsor.MemberId,
                req.SelectedProductIds ?? new List<string>(),
                now);
        }
        catch (TokenProductMismatchException ex)
        {
            // Resolve product NAMES for a more helpful UI message — IDs alone don't help the user.
            var (allowedIds, allowedNames) = await ResolveAllowedProducts(grantedLinks, ct);
            var nameList = allowedNames.Count > 0 ? string.Join(", ", allowedNames) : ex.Message;
            _logger.LogInformation("ValidateToken: code '{Code}' product mismatch", code);
            return Result<ValidateTokenResponse>.Success(new ValidateTokenResponse
            {
                Valid               = false,
                Message             = $"This token only covers: {nameList}. Please adjust your product selection.",
                AllowedProductIds   = allowedIds,
                AllowedProductNames = allowedNames,
                TokenTypeId         = tokenType.Id
            });
        }
        catch (DomainException ex)
        {
            _logger.LogInformation("ValidateToken: code '{Code}' rejected ({Code2})", code, ex.Code);
            return Generic();
        }

        // Success — return the allowed product set so the UI can constrain choices.
        var (ids, names) = await ResolveAllowedProducts(grantedLinks, ct);
        return Result<ValidateTokenResponse>.Success(new ValidateTokenResponse
        {
            Valid               = true,
            Message             = "Token is valid.",
            AllowedProductIds   = ids,
            AllowedProductNames = names,
            TokenTypeId         = tokenType.Id
        });

        static Result<ValidateTokenResponse> Generic()
            => Result<ValidateTokenResponse>.Success(new ValidateTokenResponse
            {
                Valid   = false,
                Message = GenericMessage
            });
    }

    private async Task<(List<string> Ids, List<string> Names)> ResolveAllowedProducts(
        List<TokenTypeProduct> grantedLinks, CancellationToken ct)
    {
        var ids = grantedLinks
            .Where(p => p.Role == Domain.Enums.TokenProductRole.Granted)
            .Select(p => p.ProductId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return (ids, new List<string>());

        var names = await _db.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync(ct);

        return (ids, names);
    }
}
