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
        var counts = await CountsQuery(_db, _currentUser.MemberId, _dateTime.Now).FirstOrDefaultAsync(ct);

        return Result<TokenSummaryDto>.Success(new TokenSummaryDto
        {
            TotalTokens    = counts?.Total ?? 0,
            TotalUsed      = counts?.Used ?? 0,
            TotalAvailable = counts?.Available ?? 0
        });
    }

    public sealed record SummaryCounts(int Total, int Used, int Available);

    /// <summary>
    /// Las tres cifras en UNA consulta: se agrupa todo en un solo grupo y cada cifra es un COUNT con su
    /// condición. Sin instancias no hay grupo y la consulta no devuelve fila (de ahí los ceros).
    /// </summary>
    public static IQueryable<SummaryCounts> CountsQuery(AppDbContext db, string? memberId, DateTime now) =>
        db.TokenTransactions.AsNoTracking()
            .Where(t => t.MemberId == memberId && t.ReferenceId != null)
            .GroupBy(_ => 1)
            .Select(g => new SummaryCounts(
                g.Count(),
                g.Count(t => t.Status == TokenInstanceStatus.Used),
                g.Count(t => (t.Status == TokenInstanceStatus.Issued || t.Status == TokenInstanceStatus.Distributed)
                             && (t.ExpiresAt == null || t.ExpiresAt > now))));
}
