using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedAPICenter.DTOs;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Features.ValidateMemberToken;

/// <summary>
/// Validates whether a member holds a sufficient token balance of the requested type.
///
/// Business rules:
/// - If the TokenType does not exist or is inactive, returns Failure.
/// - If no TokenBalance record exists for the member + type combination, the available
///   balance is treated as 0 (IsValid = false).
/// - IsValid = true only when AvailableBalance >= RequiredQuantity.
/// </summary>
public class ValidateMemberTokenHandler
    : IRequestHandler<ValidateMemberTokenCommand, Result<ValidateTokenResponse>>
{
    private readonly AppDbContext _db;

    public ValidateMemberTokenHandler(AppDbContext db) => _db = db;

    public async Task<Result<ValidateTokenResponse>> Handle(
        ValidateMemberTokenCommand request,
        CancellationToken ct = default)
    {
        var req = request.Request;

        // ── Verify token type exists and is active ────────────────────────────
        var tokenType = await _db.TokenTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.TokenTypeId && t.IsActive, ct);

        if (tokenType is null)
            return Result<ValidateTokenResponse>.Failure(
                "TOKEN_TYPE_NOT_FOUND",
                $"Token type '{req.TokenTypeId}' was not found or is inactive.");

        // ── Load balance (may not exist yet — treat as 0) ─────────────────────
        var balance = await _db.TokenBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.MemberId == req.MemberId && b.TokenTypeId == req.TokenTypeId,
                ct);

        var availableBalance = balance?.Balance ?? 0;
        var isValid = availableBalance >= req.RequiredQuantity;

        return Result<ValidateTokenResponse>.Success(new ValidateTokenResponse
        {
            IsValid          = isValid,
            AvailableBalance = availableBalance,
            TokenTypeName    = tokenType.Name
        });
    }
}
