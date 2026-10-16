using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Tokens;
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

    /// <summary>
    /// Las tres cifras en UNA consulta. Vive en <see cref="MemberTokenQueries.Counts"/> porque AdminAPI
    /// enseña el mismo resumen en la ficha del miembro.
    /// </summary>
    public static IQueryable<MemberTokenQueries.SummaryCounts> CountsQuery(AppDbContext db, string? memberId, DateTime now) =>
        MemberTokenQueries.Counts(db, memberId, now);
}
