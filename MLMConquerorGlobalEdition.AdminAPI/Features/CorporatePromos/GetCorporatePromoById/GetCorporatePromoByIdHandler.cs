using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.GetCorporatePromoById;

public class GetCorporatePromoByIdHandler
    : IRequestHandler<GetCorporatePromoByIdQuery, Result<CorporatePromoDto>>
{
    private readonly AppDbContext _db;

    public GetCorporatePromoByIdHandler(AppDbContext db) => _db = db;

    public async Task<Result<CorporatePromoDto>> Handle(
        GetCorporatePromoByIdQuery request, CancellationToken cancellationToken)
    {
        var promo = await _db.CorporatePromos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, cancellationToken);

        if (promo is null)
            return Result<CorporatePromoDto>.Failure("PROMO_NOT_FOUND", "Corporate promo not found.");

        var memberCount = await _db.MembershipSubscriptions
            .AsNoTracking()
            .Where(s => s.CreationDate >= promo.StartDate && s.CreationDate <= promo.EndDate)
            .Select(s => s.MemberId)
            .Distinct()
            .CountAsync(cancellationToken);

        return Result<CorporatePromoDto>.Success(new CorporatePromoDto
        {
            Id = promo.Id,
            Title = promo.Title,
            Description = promo.Description,
            StartDate = promo.StartDate,
            EndDate = promo.EndDate,
            BannerUrl = promo.BannerUrl,
            IsActive = promo.IsActive,
            MemberCount = memberCount,
            CreationDate = promo.CreationDate
        });
    }
}
