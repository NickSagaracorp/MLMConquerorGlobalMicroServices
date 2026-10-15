using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Tokens.GetTokenSummary;

/// <summary>Las tres cifras de la cabecera, sobre las instancias canjeables (con código) que hoy son del miembro.</summary>
public class GetTokenSummaryHandler : IRequestHandler<GetTokenSummaryQuery, Result<TokenSummaryDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public GetTokenSummaryHandler(AppDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<TokenSummaryDto>> Handle(GetTokenSummaryQuery request, CancellationToken ct)
    {
        var memberId  = _currentUser.MemberId;
        var now       = _dateTime.Now;
        var instances = _db.TokenTransactions.AsNoTracking().Where(t => t.MemberId == memberId && t.ReferenceId != null);

        return Result<TokenSummaryDto>.Success(new TokenSummaryDto
        {
            TotalTokens    = await instances.CountAsync(ct),
            TotalUsed      = await instances.CountAsync(t => t.Status == TokenInstanceStatus.Used, ct),
            TotalAvailable = await instances.CountAsync(t =>
                (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
                && (t.ExpiresAt == null || t.ExpiresAt > now), ct)
        });
    }
}
