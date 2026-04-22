using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.DeleteDocumentType;

public class DeleteDocumentTypeHandler : IRequestHandler<DeleteDocumentTypeCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteDocumentTypeHandler(AppDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.DocumentTypes
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (entity is null)
            return Result<bool>.Failure("NOT_FOUND", "Document type not found.");

        if (entity.Documents.Any())
            return Result<bool>.Failure("HAS_DOCUMENTS", "Cannot delete a document type that has associated documents.");

        _db.DocumentTypes.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
