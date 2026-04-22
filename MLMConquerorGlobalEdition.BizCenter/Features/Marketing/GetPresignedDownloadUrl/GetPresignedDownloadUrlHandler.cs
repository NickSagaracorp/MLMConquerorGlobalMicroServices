using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Marketing.GetPresignedDownloadUrl;

public class GetPresignedDownloadUrlHandler : IRequestHandler<GetPresignedDownloadUrlQuery, Result<string>>
{
    private readonly AppDbContext          _db;
    private readonly IS3PresignedUrlService _presigned;

    public GetPresignedDownloadUrlHandler(AppDbContext db, IS3PresignedUrlService presigned)
    {
        _db        = db;
        _presigned = presigned;
    }

    public async Task<Result<string>> Handle(GetPresignedDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var doc = await _db.MarketingDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.IsActive, cancellationToken);

        if (doc is null)
            return Result<string>.Failure("NOT_FOUND", "Document not found.");

        try
        {
            var url = await _presigned.GeneratePresignedDownloadUrlAsync(
                doc.S3Key, doc.OriginalFileName, expiryMinutes: 15, ct: cancellationToken);

            return Result<string>.Success(url);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("PRESIGN_FAILED", $"Could not generate download link: {ex.Message}");
        }
    }
}
