using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.RankEngine.DTOs;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.RankEngine.Features.GenerateCertificate;

public class GenerateCertificateHandler : IRequestHandler<GenerateCertificateCommand, Result<CertificateGenerationResponse>>
{
    private readonly AppDbContext _db;
    private readonly ICertificatePdfFillerService _pdfFiller;
    private readonly IS3FileService _s3;
    private readonly IDateTimeProvider _dateTime;
    private readonly ICurrentUserService _currentUser;

    public GenerateCertificateHandler(
        AppDbContext db,
        ICertificatePdfFillerService pdfFiller,
        IS3FileService s3,
        IDateTimeProvider dateTime,
        ICurrentUserService currentUser)
    {
        _db = db;
        _pdfFiller = pdfFiller;
        _s3 = s3;
        _dateTime = dateTime;
        _currentUser = currentUser;
    }

    public async Task<Result<CertificateGenerationResponse>> Handle(GenerateCertificateCommand command, CancellationToken ct)
    {
        // Load rank history with rank definition
        var history = await _db.MemberRankHistories
            .Include(h => h.RankDefinition)
            .FirstOrDefaultAsync(h => h.Id == command.MemberRankHistoryId && !h.IsDeleted, ct);

        if (history is null)
            return Result<CertificateGenerationResponse>.Failure(
                "RANK_HISTORY_NOT_FOUND",
                $"Rank history record '{command.MemberRankHistoryId}' not found.");

        if (history.RankDefinition is null)
            return Result<CertificateGenerationResponse>.Failure(
                "RANK_DEFINITION_MISSING",
                "Rank definition not found for this history record.");

        // Load member to get full name
        var member = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == history.MemberId, ct);

        if (member is null)
            return Result<CertificateGenerationResponse>.Failure(
                "MEMBER_NOT_FOUND",
                $"Member '{history.MemberId}' not found.");

        var rankNameSlug = history.RankDefinition.Name
            .ToLowerInvariant()
            .Replace(" ", "-");

        var templateData = new CertificateTemplateData(
            FullName: $"{member.FirstName} {member.LastName}".Trim(),
            MemberId: member.MemberId,
            RankName: history.RankDefinition.Name,
            RankDescription: history.RankDefinition.Description ?? string.Empty,
            AchievedAt: history.AchievedAt);

        var pdfBytes = await _pdfFiller.FillAsync(rankNameSlug, templateData, ct);

        var s3Key = $"certificates/{member.MemberId}/{rankNameSlug}/{history.Id}.pdf";
        using var stream = new MemoryStream(pdfBytes);
        var certificateUrl = await _s3.UploadAsync(s3Key, stream, "application/pdf", ct);

        var now = _dateTime.Now;
        history.GeneratedCertificateUrl = certificateUrl;
        history.LastUpdateDate = now;
        history.LastUpdateBy = _currentUser.UserId;

        await _db.SaveChangesAsync(ct);

        return Result<CertificateGenerationResponse>.Success(new CertificateGenerationResponse
        {
            MemberRankHistoryId = history.Id,
            MemberId = member.MemberId,
            RankName = history.RankDefinition.Name,
            CertificateUrl = certificateUrl,
            GeneratedAt = now
        });
    }
}
