using MediatR;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.CorporatePromos.CreateCorporatePromo;

public class CreateCorporatePromoHandler : IRequestHandler<CreateCorporatePromoCommand, Result<string>>
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public CreateCorporatePromoHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db = db;
        _currentUser = cu;
        _dateTime = dt;
    }

    public async Task<Result<string>> Handle(
        CreateCorporatePromoCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new CorporatePromo
        {
            Title = req.Title,
            Description = req.Description,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            BannerUrl = req.BannerUrl,
            IsActive = true,
            CreationDate = now,
            CreatedBy = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy = _currentUser.UserId
        };

        await _db.CorporatePromos.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(entity.Id);
    }
}
