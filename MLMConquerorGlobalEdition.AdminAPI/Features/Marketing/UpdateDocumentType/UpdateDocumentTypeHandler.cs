using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpdateDocumentType;

public class UpdateDocumentTypeHandler : IRequestHandler<UpdateDocumentTypeCommand, Result<DocumentTypeDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public UpdateDocumentTypeHandler(AppDbContext db, ICurrentUserService cu, IDateTimeProvider dt)
    {
        _db          = db;
        _currentUser = cu;
        _dateTime    = dt;
    }

    public async Task<Result<DocumentTypeDto>> Handle(UpdateDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (entity is null)
            return Result<DocumentTypeDto>.Failure("NOT_FOUND", "Document type not found.");

        var req = request.Request;
        entity.Name          = req.Name;
        entity.Description   = req.Description;
        entity.SortOrder     = req.SortOrder;
        entity.IsActive      = req.IsActive;
        entity.LastUpdateDate = _dateTime.Now;
        entity.LastUpdateBy  = _currentUser.UserId;

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
