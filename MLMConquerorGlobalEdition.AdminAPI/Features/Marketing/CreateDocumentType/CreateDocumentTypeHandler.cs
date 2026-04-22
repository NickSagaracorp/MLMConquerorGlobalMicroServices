using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Domain.Entities.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.CreateDocumentType;

public class CreateDocumentTypeHandler : IRequestHandler<CreateDocumentTypeCommand, Result<DocumentTypeDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public CreateDocumentTypeHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db          = db;
        _currentUser = cu;
        _dateTime    = dt;
    }

    public async Task<Result<DocumentTypeDto>> Handle(CreateDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var now = _dateTime.Now;

        var entity = new DocumentType
        {
            Name          = req.Name,
            Description   = req.Description,
            SortOrder     = req.SortOrder,
            IsActive      = true,
            CreationDate  = now,
            CreatedBy     = _currentUser.UserId,
            LastUpdateDate = now,
            LastUpdateBy  = _currentUser.UserId
        };

        await _db.DocumentTypes.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<DocumentTypeDto>.Success(new DocumentTypeDto
        {
            Id          = entity.Id,
            Name        = entity.Name,
            Description = entity.Description,
            SortOrder   = entity.SortOrder,
            IsActive    = entity.IsActive
        });
    }
}
