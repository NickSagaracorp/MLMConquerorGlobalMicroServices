using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.DeleteMarketingDocument;

public class DeleteMarketingDocumentHandler : IRequestHandler<DeleteMarketingDocumentCommand, Result<bool>>
{
    private readonly AppDbContext      _db;
    private readonly IS3StorageService _s3;

    public DeleteMarketingDocumentHandler(AppDbContext db, IS3StorageService s3)
    {
        _db = db;
        _s3 = s3;
    }

    public async Task<Result<bool>> Handle(DeleteMarketingDocumentCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.MarketingDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (entity is null)
            return Result<bool>.Failure("NOT_FOUND", "Document not found.");

        try
        {
            await _s3.DeleteAsync(entity.S3Key, cancellationToken);
        }
        catch
        {
            // S3 key may already be gone — continue with DB deletion
        }

        _db.MarketingDocuments.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
